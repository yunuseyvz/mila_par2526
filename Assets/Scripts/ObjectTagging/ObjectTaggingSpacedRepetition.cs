using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SQLite;
using UnityEngine;

namespace LanguageTutor.ObjectTagging
{
    public class ObjectTaggingSpacedRepetition : MonoBehaviour
    {
        [SerializeField] private string databaseFileName = "object_tagging.sqlite";
        [SerializeField] private float reviewCooldownSeconds = 30f;
        [SerializeField] private int minScore = 0;
        [SerializeField] private int maxScore = 10;
        [SerializeField] private bool logDiagnostics = false;

        private SQLiteConnection _db;
        private string _dbPath;
        private bool _initialized;
        private string _lastTarget;

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (_db != null)
            {
                _db.Close();
                _db = null;
            }
        }

        public string SelectNextTarget(IReadOnlyList<string> labels)
        {
            Initialize();

            var normalized = NormalizeLabels(labels);
            if (normalized.Count == 0)
            {
                _lastTarget = null;
                return null;
            }

            var items = EnsureItems(normalized);
            var now = GetUnixTimeSeconds();

            var weights = new List<(string label, float weight)>(normalized.Count);
            for (var i = 0; i < normalized.Count; i++)
            {
                var label = normalized[i];
                var item = items[label];

                var scorePenalty = 1f / (1f + item.Score);
                var recencySeconds = item.LastSeenUnix > 0 ? (float)(now - item.LastSeenUnix) : reviewCooldownSeconds;
                var recencyFactor = Mathf.Clamp01(recencySeconds / Mathf.Max(1f, reviewCooldownSeconds));
                var weight = scorePenalty * Mathf.Lerp(0.25f, 1f, recencyFactor);

                if (!string.IsNullOrEmpty(_lastTarget) && normalized.Count > 1 && label == _lastTarget)
                {
                    weight *= 0.25f;
                }

                weights.Add((label, Mathf.Max(0.01f, weight)));
            }

            var selected = WeightedPick(weights);
            if (selected == null)
            {
                return null;
            }

            MarkPrompted(items[selected], now);
            _lastTarget = selected;
            return selected;
        }

        public string BuildScoreSummary(IReadOnlyList<string> labels, int maxItems = 12)
        {
            Initialize();

            var normalized = NormalizeLabels(labels);
            if (normalized.Count == 0)
            {
                return string.Empty;
            }

            var items = EnsureItems(normalized);
            var ordered = normalized
                .Select(label => items[label])
                .OrderBy(item => item.Score)
                .ThenBy(item => item.SeenCount)
                .ThenBy(item => item.Label)
                .Take(Mathf.Max(1, maxItems))
                .ToList();

            var sb = new StringBuilder();
            for (var i = 0; i < ordered.Count; i++)
            {
                var item = ordered[i];
                sb.Append(item.Label);
                sb.Append(" (score=");
                sb.Append(item.Score);
                sb.Append(", seen=");
                sb.Append(item.SeenCount);
                sb.Append(')');

                if (i < ordered.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            return sb.ToString();
        }

        public bool EvaluateAndRecord(string userInput, string targetLabel)
        {
            if (string.IsNullOrWhiteSpace(targetLabel))
            {
                return false;
            }

            var isCorrect = ContainsLabel(userInput, targetLabel);
            RecordAttempt(targetLabel, isCorrect);
            return isCorrect;
        }

        public void RecordAttempt(string label, bool correct)
        {
            Initialize();

            var normalized = NormalizeLabel(label);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            var item = _db.Table<VocabItem>().FirstOrDefault(entry => entry.Label == normalized);
            if (item == null)
            {
                item = new VocabItem
                {
                    Label = normalized
                };
                _db.Insert(item);
            }

            var now = GetUnixTimeSeconds();
            if (correct)
            {
                item.Score = Mathf.Min(maxScore, item.Score + 1);
                item.CorrectCount += 1;
                item.LastCorrectUnix = now;
            }
            else
            {
                item.Score = Mathf.Max(minScore, item.Score - 1);
                item.IncorrectCount += 1;
                item.LastIncorrectUnix = now;
            }

            _db.Update(item);
            _db.Insert(new ReviewEvent
            {
                Label = normalized,
                IsCorrect = correct ? 1 : 0,
                TimestampUnix = now
            });

            if (logDiagnostics)
            {
                Debug.Log($"[ObjectTaggingSpacedRepetition] {normalized} -> {(correct ? "correct" : "incorrect")}, score {item.Score}");
            }
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _dbPath = Path.Combine(Application.persistentDataPath, databaseFileName);
            _db = new SQLiteConnection(
                _dbPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

            _db.CreateTable<VocabItem>();
            _db.CreateTable<ReviewEvent>();

            _initialized = true;

            if (logDiagnostics)
            {
                Debug.Log($"[ObjectTaggingSpacedRepetition] DB ready at {_dbPath}");
            }
        }

        private Dictionary<string, VocabItem> EnsureItems(List<string> labels)
        {
            var existing = _db.Table<VocabItem>().ToList();
            var map = existing.ToDictionary(entry => entry.Label, entry => entry, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                if (map.ContainsKey(label))
                {
                    continue;
                }

                var item = new VocabItem
                {
                    Label = label
                };
                _db.Insert(item);
                map[label] = item;
            }

            return map;
        }

        private void MarkPrompted(VocabItem item, long now)
        {
            item.SeenCount += 1;
            item.LastSeenUnix = now;
            _db.Update(item);
        }

        private static string WeightedPick(List<(string label, float weight)> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                return null;
            }

            var total = 0f;
            for (var i = 0; i < weights.Count; i++)
            {
                total += weights[i].weight;
            }

            if (total <= 0f)
            {
                return weights[0].label;
            }

            var roll = UnityEngine.Random.value * total;
            for (var i = 0; i < weights.Count; i++)
            {
                roll -= weights[i].weight;
                if (roll <= 0f)
                {
                    return weights[i].label;
                }
            }

            return weights[weights.Count - 1].label;
        }

        private static List<string> NormalizeLabels(IReadOnlyList<string> labels)
        {
            var normalized = new List<string>();
            if (labels == null)
            {
                return normalized;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < labels.Count; i++)
            {
                var label = NormalizeLabel(labels[i]);
                if (string.IsNullOrWhiteSpace(label) || seen.Contains(label))
                {
                    continue;
                }

                normalized.Add(label);
                seen.Add(label);
            }

            return normalized;
        }

        private static string NormalizeLabel(string label)
        {
            return string.IsNullOrWhiteSpace(label) ? null : label.Trim().ToLowerInvariant();
        }

        private static bool ContainsLabel(string text, string label)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            var normalizedText = NormalizeForMatch(text);
            var normalizedLabel = NormalizeForMatch(label);

            if (string.IsNullOrWhiteSpace(normalizedLabel))
            {
                return false;
            }

            if (normalizedLabel.Contains(" "))
            {
                return normalizedText.Contains(normalizedLabel);
            }

            return Regex.IsMatch(normalizedText, $@"\b{Regex.Escape(normalizedLabel)}\b", RegexOptions.IgnoreCase);
        }

        private static string NormalizeForMatch(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(' ');
                }
            }

            return Regex.Replace(builder.ToString(), "\\s+", " ").Trim();
        }

        private static long GetUnixTimeSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        [Table("vocab_items")]
        private class VocabItem
        {
            [PrimaryKey]
            public string Label { get; set; }

            public int Score { get; set; }
            public int SeenCount { get; set; }
            public int CorrectCount { get; set; }
            public int IncorrectCount { get; set; }
            public long LastSeenUnix { get; set; }
            public long LastCorrectUnix { get; set; }
            public long LastIncorrectUnix { get; set; }
        }

        [Table("review_events")]
        private class ReviewEvent
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }

            [Indexed]
            public string Label { get; set; }

            public int IsCorrect { get; set; }
            public long TimestampUnix { get; set; }
        }
    }
}

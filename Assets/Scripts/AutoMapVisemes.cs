using UnityEngine;

public class AutoMapVisemes : MonoBehaviour
{
    [ContextMenu("Auto Map CC4 Visemes")] // Adds a button to the component menu
    public void MapVisemes()
    {
        var morphTarget = GetComponent<OVRLipSyncContextMorphTarget>();
        var smr = GetComponent<SkinnedMeshRenderer>();

        if (morphTarget == null || smr == null) 
        {
            Debug.LogError("Missing OVR Morph Target or Skinned Mesh Renderer!");
            return;
        }

        // Standard CC4 Blendshape Names in Oculus Order (sil, PP, FF, TH, DD, kk, CH, SS, nn, RR, aa, E, ih, oh, ou)
        string[] cc4Names = new string[] {
            "",             // sil (Silence)
            "V_Explosive",  // PP
            "V_Dental_Lip", // FF
            "V_Tongue_Out", // TH
            "V_Tight_O",    // DD
            "Jaw_Open",     // kk (Approximation)
            "V_Lip_Open",   // CH
            "V_Dental_Lip", // SS
            "V_Lip_Open",   // nn
            "V_Lip_Pucker", // RR
            "V_AA",         // aa
            "V_E",          // E
            "V_IH",         // ih
            "V_OH",         // oh
            "V_OU"          // ou
        };

        // Resize array to 15
        if (morphTarget.visemeToBlendTargets.Length != 15)
            morphTarget.visemeToBlendTargets = new int[15];

        // Find indices
        for (int i = 0; i < cc4Names.Length; i++)
        {
            if (string.IsNullOrEmpty(cc4Names[i])) 
            {
                morphTarget.visemeToBlendTargets[i] = -1; // Silence
                continue;
            }

            int index = smr.sharedMesh.GetBlendShapeIndex(cc4Names[i]);
            if (index != -1)
            {
                morphTarget.visemeToBlendTargets[i] = index;
                Debug.Log($"Mapped {i} to {cc4Names[i]} (Index: {index})");
            }
            else
            {
                Debug.LogWarning($"Could not find blendshape: {cc4Names[i]}");
            }
        }
    }
}
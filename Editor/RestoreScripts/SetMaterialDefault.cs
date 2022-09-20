using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class SetMaterialDefault : MonoBehaviour
{
    public bool activate = false;
    private void OnEnable()
    {
        if (activate)
        {
            MeshRenderer rend = GetComponent<MeshRenderer>();
            if (rend != null)
            {
                Shader std = Shader.Find("Standard");
                if (rend.sharedMaterial.shader != std)
                    rend.sharedMaterial.shader = std;
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkinnedMeshHighlighter : MonoBehaviour
{
    [SerializeField] List<SkinnedMeshRenderer> meshToHighlight;
    [SerializeField] Material originalMaterial;
    [SerializeField] Material highlightMaterial;

    public void HighlightMesh(bool highlight)
    {
        foreach (var mesh in meshToHighlight)
        {
            if (highlight)
            { 
                mesh.material = highlightMaterial;
            }
            else
            {
                mesh.material = originalMaterial;
            }
        }
    }
}

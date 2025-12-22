using UnityEngine;

public class DropZone : MonoBehaviour
{
    public Transform workspaceContent;

    private void Awake()
    {
        if (workspaceContent == null) workspaceContent = transform;
    }

    public void Accept(Transform blockTransform)
    {
        blockTransform.SetParent(workspaceContent, worldPositionStays: false);
    }
}

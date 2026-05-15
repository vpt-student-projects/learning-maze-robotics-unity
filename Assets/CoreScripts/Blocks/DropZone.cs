using UnityEngine;

public enum DropZoneType
{
    Workspace,
    IfBranch,
    ElseBranch
}

public class DropZone : MonoBehaviour
{
    public Transform workspaceContent;

    public DropZoneType zoneType = DropZoneType.Workspace;

    public IfElseBlockUI ownerIfElse;

    private void Awake()
    {
        if (workspaceContent == null)
            workspaceContent = transform;
    }

    public void Accept(Transform blockTransform)
    {
        if (blockTransform == null) return;

        blockTransform.SetParent(workspaceContent, false);
        blockTransform.SetAsLastSibling();

        RectTransform rt = blockTransform.GetComponent<RectTransform>();

        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        BlockChainManager chain = FindAnyObjectByType<BlockChainManager>();

        if (chain != null)
            chain.RefreshIfElseBranches();
    }
}
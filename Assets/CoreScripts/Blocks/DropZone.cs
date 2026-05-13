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
        blockTransform.SetParent(workspaceContent, false);

        var rt = blockTransform.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = Vector2.zero;
        }

        blockTransform.SetAsLastSibling();

        var chain = FindAnyObjectByType<BlockChainManager>();
        if (chain != null)
        {
            chain.RebuildFromWorkspace(chain.workspaceRoot);
            chain.RefreshIfElseBranches();
        }
    }
}
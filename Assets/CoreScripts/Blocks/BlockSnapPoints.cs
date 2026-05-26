using UnityEngine;

public class BlockSnapPoints : MonoBehaviour
{
    public RectTransform topSnap;
    public RectTransform bottomSnap;

    public Vector3 TopWorld
    {
        get
        {
            RefreshOwnerLoopBeforeReadingSnap();

            Vector3 result = topSnap != null ? topSnap.position : transform.position;

            return result;
        }
    }

    public Vector3 BottomWorld
    {
        get
        {
            RefreshOwnerLoopBeforeReadingSnap();

            LoopBlockUI loop = GetComponent<LoopBlockUI>();

            if (loop != null)
            {
                Vector3 realBottom = loop.GetRealBottomSnapWorld();

                if (bottomSnap != null)
                    bottomSnap.position = realBottom;

                return realBottom;
            }

            Vector3 result = bottomSnap != null ? bottomSnap.position : transform.position;

            Debug.Log(
                $"[SNAP READ BOTTOM] {name} | bottomSnap={(bottomSnap != null ? bottomSnap.name : "NULL")} | world={result} | local={(bottomSnap != null ? bottomSnap.anchoredPosition.ToString() : "NULL")}"
            );

            return result;
        }
    }

    private void Awake()
    {
        SyncWithLoopBlock();
    }

    private void OnEnable()
    {
        SyncWithLoopBlock();
    }

    public void SyncWithLoopBlock()
    {
        LoopBlockUI loop = GetComponent<LoopBlockUI>();

        if (loop == null)
            return;

        if (loop.bottomSnap != null)
            bottomSnap = loop.bottomSnap;
    }

    private void RefreshOwnerLoopBeforeReadingSnap()
    {
        LoopBlockUI loop = GetComponent<LoopBlockUI>();

        if (loop == null)
            return;

        SyncWithLoopBlock();

        loop.RefreshSize();

        SyncWithLoopBlock();

        Canvas.ForceUpdateCanvases();
    }
}
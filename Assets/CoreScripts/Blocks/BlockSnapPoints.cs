using UnityEngine;

public class BlockSnapPoints : MonoBehaviour
{
    public RectTransform topSnap;
    public RectTransform bottomSnap;

    public Vector3 TopWorld => topSnap != null ? topSnap.position : transform.position;
    public Vector3 BottomWorld => bottomSnap != null ? bottomSnap.position : transform.position;
}

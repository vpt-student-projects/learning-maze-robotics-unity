using UnityEngine;

public class DropZone : MonoBehaviour
{
    [Header("Where blocks will be parented")]
    public Transform workspaceContent;

    private void Awake()
    {
        if (workspaceContent == null) workspaceContent = transform;
    }

    public void Accept(DraggableBlock block)
    {
        // Кидаем в контейнер воркспейса
        block.transform.SetParent(workspaceContent, worldPositionStays: false);

        // Можно тут сделать авто-раскладку (VerticalLayoutGroup) — и блок сам встанет в список
        // Если нет layout group — он останется там, где отпустили (но сейчас parent = content)
    }
}
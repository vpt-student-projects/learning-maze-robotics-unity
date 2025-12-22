using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BlockPaletteItem : MonoBehaviour
{
    [Header("What block it represents")]
    public BlockDefinition definition;

    [Header("Prefab to spawn in workspace")]
    public GameObject blockUIPrefab;

    [Header("Optional UI")]
    public TMP_Text titleText;
    public Image iconImage;

    private void Awake()
    {
        RefreshView();
    }

    public void RefreshView()
    {
        if (definition == null) return;

        if (titleText != null) titleText.text = definition.title;
        if (iconImage != null)
        {
            iconImage.sprite = definition.icon;
            iconImage.enabled = definition.icon != null;
        }
    }
}
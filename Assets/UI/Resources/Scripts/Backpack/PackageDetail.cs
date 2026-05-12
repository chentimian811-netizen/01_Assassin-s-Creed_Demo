using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static PackageLocalData;

public class PackageDetail : MonoBehaviour
{
    private Transform UIStars;
    private Transform UIDescription;
    private Transform UIIcon;
    private Transform UITitle;
    private Transform UILeveText;
    private Transform UISkillDescription;

    // 新增：装备按钮
    [SerializeField] private Button equipBtn;
    [SerializeField] private Text equipBtnText;

    private PackageLocalItem packageLocalData;
    private PackageTableItem packageTablesItem;
    private PackagePanel uiParent;
    private bool isEquipped = false;
    private InventoryManager inventoryManager; // 缓存引用，用于 OnDestroy 取消订阅

    private void Awake()
    {
        InitUIName();
        if (equipBtn != null)
            equipBtn.onClick.AddListener(OnEquipClick);
        // 注意：原文件中的 Test() 方法已移除，不再在 Awake 中调用
    }

    private void Start()
    {
        inventoryManager = FindFirstObjectByType<InventoryManager>();
        if (inventoryManager != null)
        {
            // 使用方法引用代替 lambda，以便 OnDestroy 中正确取消订阅
            inventoryManager.OnItemEquipped += OnInventoryChanged;
            inventoryManager.OnItemUnequipped += OnInventoryChanged;
        }
    }

    private void OnDestroy()
    {
        // 取消订阅，防止内存泄漏
        if (inventoryManager != null)
        {
            inventoryManager.OnItemEquipped -= OnInventoryChanged;
            inventoryManager.OnItemUnequipped -= OnInventoryChanged;
        }
    }

        void OnInventoryChanged(PackageLocalItem item)
        {
            RefreshEquipButton();
            // 通知 PackagePanel 刷新列表（显示装备状态图标）
            if (uiParent != null)
                uiParent.RefreshList();
        }

    private void InitUIName()
    {
        UIStars = transform.Find("Center/StartLevel");
        UIDescription = transform.Find("Center/Description");
        UIIcon = transform.Find("Center/Icon");
        UITitle = transform.Find("Top/Bg/Title");
        UILeveText = transform.Find("Button/LevelPanel/LevelText");
        UISkillDescription = transform.Find("Button/Description");
    }

    public void Refresh(PackageLocalItem packageLocalData, PackagePanel uiParent)
    {
        this.packageLocalData = packageLocalData;
        this.packageTablesItem = GameManager.Instance.GetPackageItemById(packageLocalData.id);
        this.uiParent = uiParent;

        UILeveText.GetComponent<Text>().text = string.Format("Lv.{0}/40", this.packageLocalData.level.ToString());
        UIDescription.GetComponent<Text>().text = this.packageTablesItem.description;
        UISkillDescription.GetComponent<Text>().text = this.packageTablesItem.skillDescription;
        UITitle.GetComponent<Text>().text = this.packageTablesItem.name; // 修复：原代码用 .name 设置的是 GameObject 名称，应为 .text

        Texture2D t = (Texture2D)Resources.Load(this.packageTablesItem.imagePath);
        Sprite temp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0, 0));
        UIIcon.GetComponent<Image>().sprite = temp;

        RefreshStars();
        RefreshEquipButton();
    }

    public void RefreshStars()
    {
        for (int i = 0; i < UIStars.childCount; i++)
        {
            Transform star = UIStars.GetChild(i);
            if (this.packageTablesItem.star > i)
                star.gameObject.SetActive(true);
            else
                star.gameObject.SetActive(false);
        }
    }

    void OnEquipClick()
    {
        if (packageLocalData == null) return;

        if (isEquipped)
        {
            InventoryManager.Instance.Unequip();
            ToastMessage.Show("已卸下装备");
        }
        else
        {
            bool success = InventoryManager.Instance.EquipWeapon(packageLocalData.uid);
            if (success)
                ToastMessage.Show($"已装备: {packageTablesItem.name}");
            else
                ToastMessage.Show("装备失败");
        }

        RefreshEquipButton();
    }

    void RefreshEquipButton()
    {
        if (equipBtn == null || packageLocalData == null) return;
        isEquipped = packageLocalData.isEquipped;
        equipBtnText.text = isEquipped ? "卸下" : "装备";
    }
}
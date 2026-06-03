using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopNPC : MonoBehaviour
{
    [Header("商店配置")]
    [SerializeField] private ShopConfig shopConfig;
    [Header("交互提示")]
    [SerializeField] private string promptText = "按E打开商店";

    private SphereCollider triggerCollider;
    private bool playerInRange = false;

    private void Awake()
    {
        triggerCollider = gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = 2f;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if(pc != null)
        {
            playerInRange = true;
            pc.SetNearestShopNPC(this);
            ToastMessage.Show(promptText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if(pc != null)
        {
            playerInRange = false;
            pc.SetNearestShopNPC(null);
        }
    }
    
    public void OpenShop()
    {
        if(shopConfig == null)
        {
            ToastMessage.Show("商店配置为空！");
            return;
        }

        ShopPanel panel = UIManager.Instance.OpenPanel(UIconst.ShopPanel)as ShopPanel;
        if(panel != null)
        {
            panel.OpenWithConfig(UIconst.ShopPanel,shopConfig);
        }
    }
}

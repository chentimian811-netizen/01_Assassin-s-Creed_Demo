using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopNPC : MonoBehaviour
{
    [Header("商店配置")]
    [SerializeField] private ShopConfig shopConfig;
    [Header("交互提示")]
    [SerializeField] private string promptText = "按E打开商店";
    [SerializeField] private float detectionRadius = 3f; 
    private bool playerInRange =  false;
    private PlayerController cachedPc;

    void Start()
    {
        // cachedPc = FindObjectOfType<PlayerController>();

    }

    private void Update()
    {
        if(cachedPc == null)
        {
            cachedPc = FindObjectOfType<PlayerController>(true);
            if(cachedPc == null) return;

        }
        
        float distance = Vector3.Distance(transform.position,cachedPc.transform.position);

        if(distance <= detectionRadius)
        {
            if (!playerInRange)
            {
                playerInRange = true;
                cachedPc.SetNearestShopNPC(this);
                ToastMessage.Show(promptText);
            }
        }
        else
        {
            if (playerInRange)
            {
                playerInRange = false;
                cachedPc.SetNearestShopNPC(null);
            }
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position,detectionRadius);
    }
}

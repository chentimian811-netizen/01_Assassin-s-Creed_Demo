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
    private PlayerController cachedPC;


    private void Update()
    {
        if(cachedPC == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if(player != null)
            {
                Debug.Log("找到Player");
                cachedPC = player.GetComponent<PlayerController>();
            }
            else
            {
                Debug.LogError("未找到Player");
            }
            if(cachedPC == null)return;
        }

        float distance = Vector3.Distance(transform.position,cachedPC.transform.position);

        if(distance <= detectionRadius)
        {
            if (!playerInRange)
            {
                playerInRange = true;
                cachedPC.SetNearestShopNPC(this);
                ToastMessage.Show(promptText);
            }
        }
        else
        {
            if (playerInRange)
            {
                playerInRange = false;
                cachedPC.SetNearestShopNPC(null);
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

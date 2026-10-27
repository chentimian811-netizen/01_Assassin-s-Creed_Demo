    using UnityEngine;

public class ShopNPC : MonoBehaviour
{
    [Header("商店配置")]
    [SerializeField] private int shopKeeperId = 1;
    [Header("交互提示")]
    [SerializeField] private string promptText = "按E打开商店";
    [SerializeField] private float detectionRadius = 3f;
    private bool playerInRange = false;
    private PlayerController cachedPC;

    private void Update()
    {
        if (cachedPC == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                cachedPC = player.GetComponent<PlayerController>();  
                Debug.Log("发现玩家");
            }
            else
            {
                Debug.Log("未发现玩家");
            }
            
            if (cachedPC == null) return;
        }

        float distance = Vector3.Distance(transform.position, cachedPC.transform.position);
        if (distance <= detectionRadius)
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

    /// <summary>
    /// 打开商店界面。keeperId 通过 ShopFormArgs 走 UIRouter 的 userData，
    /// 不再「先 OpenPanel 再 OpenWithConfig」两步走（GF 池化后没有这种时序保证）。
    /// </summary>
    public void OpenShop()
    {
        UIRouter.Open(UIPaths.ShopForm, "Popup", new ShopFormArgs { KeeperId = shopKeeperId });
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
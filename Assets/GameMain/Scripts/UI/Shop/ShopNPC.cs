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
            if (player != null) cachedPC = player.GetComponent<PlayerController>();
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

    public void OpenShop()
    {
        ShopPanel panel = UIManager.Instance.OpenPanel(UIconst.ShopPanel) as ShopPanel;
        if (panel != null)
            panel.OpenWithConfig(UIconst.ShopPanel, shopKeeperId);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
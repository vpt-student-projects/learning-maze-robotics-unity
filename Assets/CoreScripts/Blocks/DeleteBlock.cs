using UnityEngine;

public class DeleteBlock : MonoBehaviour
{
    public void DeleteSelf()
    {
         Destroy(transform.parent.gameObject);
    }
    
}

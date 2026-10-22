using System.Collections;
using UnityEngine;

namespace BackRock { 

    public class DeactivateObject : MonoBehaviour
    {
        [SerializeField]
        private float timeToDeactivate = 3f;

        void OnEnable()
        {
            StartCoroutine(DisableObject());
        }


        IEnumerator DisableObject()
        {
            yield return new WaitForSeconds(timeToDeactivate);
            gameObject.SetActive(false);
        }

    }

}

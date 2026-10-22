using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace BackRock 
{ 
    public class UIManager : MonoBehaviour
    {
        public Image hitMarkerImage; // Reference to the hit marker UI image
        public float hitMarkerDuration = 0.2f; // Duration for how long the hit marker stays visible
        public Color normalHitColor = Color.white; // Default hit marker color
        public Color killHitColor = Color.red; // Color for kill hit marker (red)

        private Coroutine hitMarkerCoroutine;

        public GameObject hudCanvas;
        public GameObject wheelCanvas;

        private void Start()
        {
            if (hitMarkerImage != null)
            {
                hitMarkerImage.canvasRenderer.SetAlpha(0f); // Initially hide the hit marker
                hitMarkerImage.color = normalHitColor; // Set default color
            }
        }


        private void Update()
        {
            DeactivateCanvasObjects();
        }

        // Method to display the hit marker
        public void ShowHitMarker(bool isKill = false)
        {
            if (hitMarkerCoroutine != null)
            {
                // Stop the previous coroutine to avoid the marker being hidden prematurely
                StopCoroutine(hitMarkerCoroutine);
            }

            if (hitMarkerImage != null)
            {
                // Set the color based on whether it's a kill or normal hit
                hitMarkerImage.color = isKill ? killHitColor : normalHitColor;

                // Ensure the hit marker is fully visible
                hitMarkerImage.canvasRenderer.SetAlpha(1f);
                hitMarkerCoroutine = StartCoroutine(HideHitMarker());
            }
        }

        public void DeactivateCanvasObjects() 
        {
            WeaponWheelController wController = wheelCanvas.GetComponent<WeaponWheelController>();
            if (wController.weaponWheelSelected)
                hudCanvas.SetActive(false);
            else
                hudCanvas.SetActive(true);
        }

        // Coroutine to hide the hit marker after a delay
        private IEnumerator HideHitMarker()
        {
            yield return new WaitForSeconds(hitMarkerDuration);

            if (hitMarkerImage != null)
            {
                hitMarkerImage.canvasRenderer.SetAlpha(0f); // Hide hit marker after the duration
                hitMarkerImage.color = normalHitColor; // Reset to the default hit color
            }

            // Nullify the coroutine reference after it's finished
            hitMarkerCoroutine = null;
        }
    }
}
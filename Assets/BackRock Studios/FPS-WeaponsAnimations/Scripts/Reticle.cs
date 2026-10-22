using UnityEngine;
using UnityEngine.UI;

namespace BackRock
{
    public class Reticle : MonoBehaviour
    {
        private RectTransform reticle;
        private Image[] childImages;

        public float idleSize = 100f;
        public float maxSize = 200f;
        public float aimingSize = 20f; // Size when aiming
        public float sizeSpeed = 5f;
        public float aimingAlpha = 0f;
        public float normalAlpha = 1f;
        public float alphaSpeed = 5f;
        private float currentSize;
        private float currentAlpha;

        private void Start()
        {

            //Cursor.visible = false;
            //Cursor.lockState = CursorLockMode.Locked;
            
            InitializeReticle();
            
        }

        private void InitializeReticle()
        {
            reticle = GetComponent<RectTransform>();
            childImages = GetComponentsInChildren<Image>();
            currentSize = idleSize;
            currentAlpha = normalAlpha;
            SetAlpha(currentAlpha); // Set initial alpha

        }

        private void OnEnable()
        {
            InitializeReticle(); // Reinitialize when the object is enabled
        }

        private void OnDisable()
        {
            // Optional: Reset reticle state when disabled
            currentSize = idleSize;
            currentAlpha = normalAlpha;
            SetAlpha(currentAlpha);
        }

        private void Update()
        {
            // Determine target size and alpha based on input
            float targetSize = idleSize;
            float targetAlpha = normalAlpha;

            if (PackInput.GetButton("Fire2")) // Right click for aiming
            {
                targetSize = aimingSize;
                targetAlpha = aimingAlpha;
            }
            else if (isMoving())
            {
                targetSize = maxSize;
            }

            // Lerp size
            currentSize = Mathf.Lerp(currentSize, targetSize, Time.deltaTime * sizeSpeed);
            reticle.sizeDelta = new Vector2(currentSize, currentSize);

            // Lerp alpha
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * alphaSpeed);
            SetAlpha(currentAlpha);
        }

        public bool isMoving()
        {
            return PackInput.GetAxis("Horizontal") != 0 ||
                   PackInput.GetAxis("Vertical") != 0 ||
                   PackInput.GetAxis("Mouse X") != 0 ||
                   PackInput.GetAxis("Mouse Y") != 0;
        }

        private void SetAlpha(float alpha)
        {
            foreach (Image img in childImages)
            {
                Color color = img.color;
                color.a = alpha;
                img.color = color;
            }
        }
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace BackRock
{
    public class DestructibleObject : MonoBehaviour
    {
        private Rigidbody rb;
        private Material material;
        private string colorProperty;
        private Color originalColor;
        public float fadeDuration = 2f; // Time it takes to fade out
        private bool isFading = false;

        private void Start()
        {
            // Get the Rigidbody component
            rb = GetComponent<Rigidbody>();

            // Get the material of the object
            material = GetComponent<Renderer>().material;

            // Store the original color of the material. URP's Lit shader calls
            // it _BaseColor, the Built-in Standard shader _Color.
            colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor"
                          : material.HasProperty("_Color") ? "_Color" : null;
            originalColor = colorProperty != null ? material.GetColor(colorProperty) : Color.white;
        }

        private void Update()
        {
            // Check if Rigidbody is kinematic and if we haven't already started fading
            if (!rb.isKinematic && !isFading)
            {
                // Change rendering mode to transparent
                SetMaterialTransparent();

                // Start fading the material
                StartCoroutine(FadeOut());
            }
        }

        // Set the material's rendering mode to Transparent
        private void SetMaterialTransparent()
        {
            if (material.HasProperty("_Surface"))
            {
                // URP Lit: Surface Type Transparent, Blending Mode Alpha
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetShaderPassEnabled("DepthOnly", false);
            }
            else
            {
                // Built-in Standard
                material.SetFloat("_Mode", 3); // 3 = Transparent
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        // Coroutine to gradually fade out the material
        private IEnumerator FadeOut()
        {
            isFading = true;
            float elapsedTime = 0f;

            // Loop until the object becomes fully transparent
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;

                // Calculate the new alpha value based on time
                float alpha = Mathf.Lerp(originalColor.a, 0f, elapsedTime / fadeDuration);

                // Apply the new color with the adjusted alpha value
                if (colorProperty != null)
                    material.SetColor(colorProperty, new Color(originalColor.r, originalColor.g, originalColor.b, alpha));

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}

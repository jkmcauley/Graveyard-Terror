using UnityEngine;
namespace BackRock
{

    [RequireComponent(typeof(CharacterController))]
    public class FPSMovement : MonoBehaviour
    {
        public float speed = 5.0f;
        public float lookSpeed = 2.0f;
        public float jumpSpeed = 8.0f;
        public float gravity = 20.0f;

        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;
        private float rotationX = 0.0f;
        private float rotationY = 0.0f;

        public static bool canMoveCamera = true;

        void Start()
        {
            characterController = GetComponent<CharacterController>();
         
        }

        void Update()
        {
            if (canMoveCamera)
            {
                // Mouse look
                rotationX += PackInput.GetAxis("Mouse X") * lookSpeed;
                rotationY -= PackInput.GetAxis("Mouse Y") * lookSpeed;
                rotationY = Mathf.Clamp(rotationY, -90, 90);

                transform.localRotation = Quaternion.Euler(0, rotationX, 0);
                Camera.main.transform.localRotation = Quaternion.Euler(rotationY, 0, 0);
            }
        

            // Movement
            if (characterController.isGrounded)
            {
                moveDirection = new Vector3(PackInput.GetAxis("Horizontal"), 0, PackInput.GetAxis("Vertical"));
                moveDirection = transform.TransformDirection(moveDirection);
                moveDirection *= speed;

                if (PackInput.GetButton("Jump"))
                {
                    moveDirection.y = jumpSpeed;
                }
            }

            // Apply gravity
            moveDirection.y -= gravity * Time.deltaTime;

            // Move the controller
            characterController.Move(moveDirection * Time.deltaTime);
        }
    }
}
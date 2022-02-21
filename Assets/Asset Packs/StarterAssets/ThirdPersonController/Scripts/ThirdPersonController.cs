using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif


namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        [SerializeField] float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        [SerializeField] float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        [SerializeField] float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        [SerializeField] float SpeedChangeRate = 10.0f;


        [Space(10)]
        [Tooltip("The height the player can jump")]
        [SerializeField] float JumpHeight = 1.2f;

        [Tooltip("The amount times the player can jump")]
        [SerializeField] int JumpCount = 2;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        [SerializeField] float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        [SerializeField] float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        [SerializeField] float FallTimeout = 0.15f;


        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        [SerializeField] float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        [SerializeField] float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        [SerializeField] LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        [SerializeField] GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        [SerializeField] float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        [SerializeField] float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        [SerializeField] float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        [SerializeField] bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private int _extraJumpsCount;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();

            AssignAnimationIDs();
            ResetTimers();
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            Jump();
            ApplyGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void ResetTimers()
        {
            ResetJumpTimeout();
            ResetFallTimeout();
        }

        private void ResetFallTimeout()
        {
            _fallTimeoutDelta = FallTimeout;
        }

        private void ResetJumpTimeout()
        {
            _jumpTimeoutDelta = JumpTimeout;
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            SetGroundedAnimationFlag();
        }

        private void SetGroundedAnimationFlag()
        {
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                _cinemachineTargetYaw += _input.look.x * Time.deltaTime;
                _cinemachineTargetPitch += _input.look.y * Time.deltaTime;
            }

            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
        }

        private void ChangeSpeed(float targetSpeed, float inputMagnitude)
        {
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }
        }

        private void SetSpeedAnimationFlag(float targetSpeed, float inputMagnitude)
        {
            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void RotatePlayer()
        {
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);

                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }
        }

        private void Move()
        {
            // float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            float targetSpeed = SprintSpeed;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            ChangeSpeed(targetSpeed, inputMagnitude);

            RotatePlayer();

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            Vector3 jumpForce = new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime;
            _speed *= Time.deltaTime;

            _controller.Move(targetDirection.normalized * _speed + jumpForce);

            SetSpeedAnimationFlag(targetSpeed, inputMagnitude);
        }

        private void SetJumpAnimationFlag(bool isJumping, bool isFreeFalling)
        {
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump, isJumping);
                _animator.SetBool(_animIDFreeFall, isFreeFalling);
            }
        }

        private void CalculateJumpForce()
        {
            // the square root of H * -2 * G = how much velocity needed to reach desired height
            _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

        }

        private void ResetExtraJumps()
        {
            _extraJumpsCount = 0;
        }

        private bool haveReachedTimeout(float timer)
        {
            return timer <= 0.0f;
        }

        private void IncreaseTimer(float timer)
        {
            if (!haveReachedTimeout(timer))
            {
                _jumpTimeoutDelta -= Time.deltaTime;
            }
        }

		private void CheckFallTimeout()
		{
			IncreaseTimer(_fallTimeoutDelta);

			if (haveReachedTimeout(_fallTimeoutDelta))
			{
				SetJumpAnimationFlag(false, true);
			}
		}

        private void Jump()
        {
            if (Grounded)
            {
                ResetFallTimeout();
                ResetExtraJumps();

                SetJumpAnimationFlag(false, false);

                if (_input.jump && haveReachedTimeout(_jumpTimeoutDelta))
                {
                    CalculateJumpForce();
                    SetJumpAnimationFlag(true, false);
                    _input.jump = false;
                }

                IncreaseTimer(_jumpTimeoutDelta);
            }
            else
            {
                ResetJumpTimeout();

                if (_input.jump && _extraJumpsCount < JumpCount)
                {
                    CalculateJumpForce();
                    _extraJumpsCount++;
                    ResetFallTimeout();

                    SetJumpAnimationFlag(true, false);
                }

                CheckFallTimeout();

                _input.jump = false;
            }
        }

        private void ApplyGravity()
        {
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
            else if (_verticalVelocity < 0.0f)
            {
                _verticalVelocity = -2f;
            }
        }

        private float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
        }
    }
}
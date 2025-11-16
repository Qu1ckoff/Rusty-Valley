using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float gravity = -9.81f;
    public float jumpHeight = 2f;

    [Header("Stamina")]
    public float maxStamina = 5f; // секунд бега
    public float staminaRegenRate = 1f;
    public float staminaDecreaseRate = 1f;
    private float currentStamina;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;

    [Header("UI")]
    public StaminaUI staminaUI; // Ссылка на UI компонент

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = maxStamina;
        staminaUI.SetMaxStamina(maxStamina);
    }

    void Update()
    {
        // --- Проверка земли ---
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // --- Движение ---
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = transform.right * h + transform.forward * v;

        // --- Бег и стамина ---
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && (h != 0 || v != 0);

        // Расход стамины, если пытаемся бежать
        if (wantsToRun && currentStamina > 0f)
        {
            currentStamina -= staminaDecreaseRate * Time.deltaTime;
            if (currentStamina < 0f)
                currentStamina = 0f;
        }
        // Восстановление стамины только если Shift не зажат
        else if (!Input.GetKey(KeyCode.LeftShift))
        {
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina > maxStamina)
                    currentStamina = maxStamina;
            }
        }

        // Определяем реально можно ли бежать
        bool canRun = currentStamina > 0f;
        bool isRunning = wantsToRun && canRun;
        float speed = isRunning ? runSpeed : walkSpeed;

        controller.Move(move * speed * Time.deltaTime);

        // --- Прыжок ---
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // --- Гравитация ---
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // --- UI стамины ---
        staminaUI.UpdateStamina(currentStamina);
    }
}

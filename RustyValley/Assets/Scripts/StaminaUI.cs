using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    public GameObject uiPanel; // объект панели UI
    public Image fillImage;     // Image с типом Fill
    public Color fullColor = Color.cyan; // полная стамина
    public Color emptyColor = Color.red; // пустая стамина

    private float maxStamina;
    private float lastValue = -1f;

    public void SetMaxStamina(float stamina)
    {
        maxStamina = stamina;
        UpdateStamina(stamina, true);
    }

    public void UpdateStamina(float currentStamina, bool forceShow = false)
    {
        float fill = currentStamina / maxStamina;

        // Градиент цвета
        fillImage.color = Color.Lerp(emptyColor, fullColor, fill);

        // Показываем панель, если стамина не полная или есть изменения
        if (forceShow || fill < 1f || Mathf.Abs(fill - lastValue) > 0.001f)
            uiPanel.SetActive(true);
        else
            uiPanel.SetActive(false);

        fillImage.fillAmount = fill;
        lastValue = fill;
    }
}

using UnityEngine;

public class PlayAnimationOnce : MonoBehaviour
{
    public Animator animator;
    public string closeAnimationName = "BlockPanelClose";
    public string openAnimationName = "BlockPanelClose2";
    private bool isClosed = false;

    // Длительность анимации (у тебя была 0.583)
    private float animationDuration = 0.583f;
    void Start()
    {
        animator.Rebind(); // Сброс аниматора
        animator.speed = 0; // Ставим на паузу сразу
    }
    public void TogglePanel()
    {
        CancelInvoke();

        if (isClosed)
        {
            // Открываем
            animator.Play(openAnimationName, 0, 0f);
            animator.speed = 1;
            isClosed = false;
            // Через длительность анимации заморозим объект
            Invoke("FreezeAnimation", 0.883f);
        }
        else
        {
            // Закрываем
            animator.Play(closeAnimationName, 0, 0f);
            animator.speed = 1;
            isClosed = true;
            Invoke("FreezeAnimation", animationDuration);
        }
    }

    void FreezeAnimation()
    {
        // Останавливаем анимацию, объект остаётся на месте
        animator.speed = 0;
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterMover : MonoBehaviour
{
    public float speed = 3f;
    public Image priorityIndicator;
    private Animator animator;
    private Coroutine moveRoutine;
    private bool isMoving;

    void Awake()
    {
        animator = GetComponent<Animator>();
        SetMoving(false);
    }

    public void SetPriorityColor(Color color)
    {
        if (priorityIndicator != null)
            priorityIndicator.color = color;
    }

    public void MoveAlongPath(List<Transform> path, System.Action onComplete)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(FollowPath(path, onComplete));
    }

    private IEnumerator FollowPath(List<Transform> path, System.Action onComplete)
    {
        SetMoving(true);
        foreach (var point in path)
        {
            while (Vector3.Distance(transform.position, point.position) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, point.position, speed * Time.deltaTime);
                yield return null;
            }
        }
        SetMoving(false);
        onComplete?.Invoke();
    }

    private void SetMoving(bool value)
    {
        isMoving = value;
        if (animator != null)
            animator.SetBool("isMoving", isMoving);
    }

    public void ForceStop()
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);
        SetMoving(false);
    }
}

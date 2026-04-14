using System;
using UnityEngine;

public class EnemyAnimationEventHandler : MonoBehaviour
{
    private EnemyController ec;

    private void Awake()
    {
        ec = GetComponentInParent<EnemyController>();
    }

    public void OnAttack()
    {
        ec.DealSimpleMeleeAttack();
    }
}

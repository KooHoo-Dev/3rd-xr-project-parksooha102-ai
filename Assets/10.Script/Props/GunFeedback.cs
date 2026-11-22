using System;
using UnityEngine;

namespace Game
{
    // 총의 움직이는 부품 연출 (해머 · 트리거 · 실린더). Gun 과 같은 오브젝트에 붙인다.
    //   판정도 입력도 하지 않는다. Gun 이 알려 주는 상태(트리거를 얼마나 당겼나, 해머가 얼마나 젖혀졌나,
    //   실린더가 몇 칸 돌았나)를 매 프레임 읽어서 부품 각도로 보여 주기만 한다.
    //
    //   LateUpdate 에서 그리는 이유 : Gun 이 Update 에서 이번 프레임 상태를 다 정한 뒤에 읽어야 한다.
    //     해머가 떨어지는 프레임에는 Gun 의 HammerCock 이 0 이 되어 있으니, 그 프레임에 바로 "탁" 하고 내려앉는다
    [RequireComponent(typeof(Gun))]
    public class GunFeedback : MonoBehaviour
    {
        [Header("움직이는 부품 (모델의 자식)")]
        [SerializeField] private Transform hammer;     // SM_Wep_Revolver_01_Hammer
        [SerializeField] private Transform trigger;    // SM_Wep_Revolver_01_Trigger
        [SerializeField] private Transform cylinder;   // SM_Wep_Revolver_01_Cylander (비워 두면 안 돌린다)

        [Header("각도")]
        [SerializeField] private float hammerCockAngle = -45f;               // 해머가 끝까지 젖혀진 각도 (로컬 X 축)
        [SerializeField] private float triggerPullAngle = 25f;               // 트리거를 끝까지 당긴 각도 (로컬 X 축)
        [SerializeField] private float chamberAngle = 60f;                   // 실린더 한 칸 (6연발 = 60°, 로컬 Z 축)
        [SerializeField, Range(0.1f, 1f)] private float cylinderLockAt = 0.8f;   // 해머가 여기까지 젖혀지면 실린더가 한 칸을 다 돈다

        private Gun gun;

        private Quaternion hammerRest;
        private Quaternion triggerRest;
        private Quaternion cylinderRest;

        private void Awake()
        {
            gun = GetComponent<Gun>();

            if (hammer == null || trigger == null)
                throw new Exception($"[GunFeedback] {name} 의 Hammer / Trigger 칸이 비어 있습니다. 모델의 SM_Wep_Revolver_01_Hammer, _Trigger 를 꽂아 주세요.");

            // 제자리 각도를 기억해 둔다. 젖힘 · 당김은 이 각도에서 얼마나 더 돌았나로 계산한다
            hammerRest = hammer.localRotation;
            triggerRest = trigger.localRotation;
            if (cylinder != null)
                cylinderRest = cylinder.localRotation;
        }

        private void LateUpdate()
        {
            // ① 트리거 : 손가락이 당긴 만큼
            trigger.localRotation = triggerRest * Quaternion.Euler(triggerPullAngle * gun.TriggerPull, 0f, 0f);

            // ② 해머 : 젖혀진 만큼 (떨어진 프레임부터는 0 = 제자리)
            hammer.localRotation = hammerRest * Quaternion.Euler(hammerCockAngle * gun.HammerCock, 0f, 0f);

            // ③ 실린더 : 해머보다 먼저 한 칸을 다 돌고 잠긴다 (해머가 떨어질 때 약실이 총열과 맞아 있어야 하니까)
            if (cylinder != null)
            {
                float turn = Mathf.Clamp01(gun.HammerCock / cylinderLockAt);
                cylinder.localRotation = cylinderRest * Quaternion.Euler(0f, 0f, chamberAngle * (gun.ChamberIndex + turn));
            }
        }
    }
}

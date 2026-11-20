using System.Collections;
using UnityEngine;

namespace Game
{
    // 정오의 해 (기획서 5장). 1라운드 대치 동안 해를 천천히 올려, 신호(종)가 울리는 순간 정확히 머리 위에 오게 한다.
    [RequireComponent(typeof(Light))]
    public class NoonSun : MonoBehaviour
    {
        // 교안 SunCycle 과 같은 방식 : X축 각도 = 해의 높이, Y축 각도(yaw) = 해가 있는 방향
        //   씬에 놓인 X축 각도가 아침 해(시작 각도)가 된다
        [SerializeField] private float noonAngle = 90f;   // 정오 각도. 90 = 바로 머리 위

        private float startAngle;   // 씬에 놓인 X축 각도 (아침 해)
        private float yaw;          // 해가 있는 방향 (Y축 각도). 올리는 동안 바꾸지 않는다

        private void Awake()
        {
            // 씬에 놓인 처음 각도를 기억해 둔다 (교안 SunCycle 도 Awake 에서 yaw 를 적는다)
            startAngle = transform.eulerAngles.x;
            yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            StageManager.Instance.RoundStarted += HandleRoundStarted;
            StageManager.Instance.DrawSignaled += HandleDrawSignaled;
        }

        private void OnDisable()
        {
            StageManager.Instance.RoundStarted -= HandleRoundStarted;
            StageManager.Instance.DrawSignaled -= HandleDrawSignaled;
        }

        // ① 대치 시작 → 1라운드만 해를 올린다 (기획서 : 2라운드부터는 해가 이미 정점)
        //    StageManager 는 이 방송 전에 신호 시각(SignalAt)을 정해 둔다
        private void HandleRoundStarted(int round)
        {
            if (round != 1) return;

            StartCoroutine(RiseRoutine(StageManager.Instance.SignalAt));
        }

        // ② 정오 신호 → 해는 정수리 위 (마지막 프레임에 조금 모자랐어도 딱 맞춘다)
        private void HandleDrawSignaled()
        {
            StopAllCoroutines();
            SetAngle(noonAngle);
        }

        // 지금부터 신호 시각(signalAt)까지 해를 시작 각도에서 정오 각도로 올린다
        //   대치 시간은 매번 다르다 (1라운드 5~8초). 그래서 올리는 빠르기도 매번 다르다
        private IEnumerator RiseRoutine(float signalAt)
        {
            float riseStart = Time.time;
            float riseTime = signalAt - riseStart;   // 대치 시작부터 신호까지 (초)

            while (Time.time < signalAt)
            {
                // 지금까지 얼마나 왔나 (0 → 1). 같은 빠르기로 올려서, 종이 울리는 순간 딱 멈춘다
                float t = (Time.time - riseStart) / riseTime;
                SetAngle(Mathf.Lerp(startAngle, noonAngle, t));
                yield return null;
            }
        }

        // 해의 높이를 angle 로 맞춘다 (방향 yaw 는 그대로)
        private void SetAngle(float angle)
        {
            transform.rotation = Quaternion.Euler(angle, yaw, 0f);
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Game
{
    // 정오의 해 (기획서 5장). 1라운드 대치 동안 해를 정점 쪽으로 올려 그림자를 짧게 만들고, 신호 순간에는 반드시 정점에 둔다.
    [RequireComponent(typeof(Light))]
    public class NoonSun : MonoBehaviour
    {
        // 교안 SunCycle 과 같은 방식 : X축 각도 = 해의 높이, Y축 각도(yaw) = 해가 있는 방향
        //   씬에 놓인 X축 각도가 아침 해(시작 각도)가 된다
        [SerializeField] private float noonAngle = 85f;   // 정오 각도. 90 이면 바로 머리 위인데, 그러면 그림자가 몸 밑에 숨고 건물 벽이 다 같은 밝기라 85 로 둔다
        [SerializeField] private float riseTime = 3f;     // 시작 각도 → 정오 각도까지 걸리는 시간 (초)
                                                          //   1라운드 대치는 5~8초. 3초 동안 올리면 남는 2~5초가 기획서의 '고조(화면 변화 없음)' 구간이 된다

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
        private void HandleRoundStarted(int round)
        {
            if (round != 1) return;

            StartCoroutine(RiseRoutine());
        }

        // ② 정오 신호 → 해는 정수리 위 (아직 올리는 중이었어도 바로 정점으로)
        private void HandleDrawSignaled()
        {
            StopAllCoroutines();
            SetAngle(noonAngle);
        }

        // 시작 각도에서 정오 각도까지 riseTime 초 동안 올린다
        private IEnumerator RiseRoutine()
        {
            float elapsed = 0f;
            while (elapsed < riseTime)
            {
                elapsed += Time.deltaTime;

                // 지금까지 얼마나 왔나 (0 → 1). SmoothStep 은 처음과 끝을 천천히 해서 해가 툭 움직이기 시작하지 않게 한다
                float t = Mathf.SmoothStep(0f, 1f, elapsed / riseTime);
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

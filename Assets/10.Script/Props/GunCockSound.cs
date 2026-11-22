using UnityEngine;

namespace Game
{
    // 해머 젖히는 소리. 트리거를 당기는 동안 해머 · 실린더가 움직이는 대로 "철컥 … 딸깍" 한다.
    //   Gun 과 같은 오브젝트에 붙인다. GunFeedback 처럼 Gun 의 상태(HammerCock)만 읽고, 입력도 판정도 하지 않는다
    //
    //   더블 액션 한 번 당기기 (Gun 설명 참고) 를 소리로 나누면
    //     ① 해머가 움직이기 시작 (HammerCock 이 startAt 을 넘는 순간) : "철컥" — 해머가 뒤로 젖혀진다
    //     ② 실린더가 잠김       (HammerCock 이 lockAt 을 넘는 순간)  : "딸깍" — 다음 약실이 총열에 맞물린다
    //     ③ 해머가 떨어짐 → 총성 (GunEffects) 또는 헛방아쇠 (Gun 의 dryFireClip)
    //   천천히 당기면 철컥과 딸깍 사이가 벌어지고, 확 당기면 붙어서 한 번에 "철컥딸깍" 이 된다
    //
    //   트리거를 반쯤 당겼다 놓으면 해머도 제자리로 돌아간다 → 다시 당기면 다시 소리가 난다
    //     문턱에서 손가락이 떨려도 연달아 나지 않게, 문턱보다 rearmGap 만큼 내려가야 다시 울린다
    [RequireComponent(typeof(Gun))]
    public class GunCockSound : MonoBehaviour
    {
        [Header("클립")]
        [SerializeField] private AudioClip startClip;   // HammerCockStart (철컥)
        [SerializeField] private AudioClip lockClip;    // HammerCockLock (딸깍)

        [Header("문턱 (HammerCock 0 ~ 1)")]
        [SerializeField, Range(0f, 1f)] private float startAt = 0.15f;
        [SerializeField, Range(0f, 1f)] private float lockAt = 0.8f;    // GunFeedback 의 Cylinder Lock At 과 같게 (실린더가 다 도는 순간)
        [SerializeField, Range(0f, 0.5f)] private float rearmGap = 0.1f;

        [Header("설정")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;     // 손 안의 작은 기계 소리. 총성보다 한참 작게
        [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        private Gun gun;
        private AudioSource audioSource;
        private bool startArmed = true;   // true = 문턱을 넘으면 울릴 준비가 됐다
        private bool lockArmed = true;

        private void Awake()
        {
            gun = GetComponent<Gun>();

            // 소리는 손에 쥔 총에서 나야 한다 → 총에 3D AudioSource 를 단다 (없으면 만든다)
            //   총구(Muzzle)의 AudioSource 는 총성용이라 10m 밖에서도 들리게 Min Distance 가 크다. 그래서 따로 쓴다
            if (TryGetComponent(out audioSource) == false)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.minDistance = 0.3f;   // 손 → 귀 0.3 ~ 0.6m
            }
            audioSource.playOnAwake = false;
            SoundManager.Route(audioSource, SoundChannel.Sfx);
        }

        // GunFeedback 처럼 LateUpdate : Gun 이 Update 에서 이번 프레임 HammerCock 을 정한 뒤에 읽는다
        private void LateUpdate()
        {
            float cock = gun.HammerCock;

            startArmed = Check(cock, startAt, startArmed, startClip);
            lockArmed = Check(cock, lockAt, lockArmed, lockClip);
        }

        // 준비된 상태에서 문턱을 넘으면 울리고 잠근다. 문턱보다 rearmGap 아래로 내려가면 다시 준비
        //   해머가 떨어지면 HammerCock 이 0 이 되므로 저절로 둘 다 다시 준비된다
        private bool Check(float cock, float threshold, bool armed, AudioClip clip)
        {
            if (armed && cock >= threshold)
            {
                Play(clip);
                return false;
            }
            if (armed == false && cock < threshold - rearmGap)
                return true;
            return armed;
        }

        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}

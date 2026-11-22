using UnityEngine;

namespace Game
{
    // 정오의 종 (기획서 5장). 신호 순간 종이 한 번 울린다. 공식 신호는 이 소리 하나뿐이다.
    //   AudioSource : AudioClip = NoonBell, Play On Awake 끔
    //   Spatial Blend 를 0(2D)으로 두면 어디를 보고 있든 똑같이 크게 들린다
    [RequireComponent(typeof(AudioSource))]
    public class NoonBell : MonoBehaviour
    {
        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            SoundManager.Route(audioSource, SoundChannel.Sfx);   // 신호라도 효과음 갈래. 효과음을 0 으로 하면 종도 안 들린다
        }

        private void OnEnable()
        {
            StageManager.Instance.DrawSignaled += HandleDrawSignaled;
        }

        private void OnDisable()
        {
            StageManager.Instance.DrawSignaled -= HandleDrawSignaled;
        }

        // 정오 신호 → 종 1회
        private void HandleDrawSignaled()
        {
            audioSource.Play();
        }
    }
}

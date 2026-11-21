using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Table
{
    // 난이도. enemies.json 의 "Difficulty" 글자와 이름이 같아야 한다.
    public enum Difficulty
    {
        Easy,     // 쉬움
        Normal,   // 보통
        Hard,     // 어려움
    }

    // enemies.json 의 모양 (3차원 표)
    //
    //   [                                  ← 난이도 목록
    //     {                                ← 적 한 명 (= 난이도 하나)   → EnemyDTO
    //       "Difficulty": "Easy",
    //       "PrefabKey": "...",
    //       "Name": "풋내기 조니",          ← 수배지에 적는 현상범 이름
    //       "BaseScore": 700,             ← 이 적을 이기면 받는 기본 점수 (라운드마다 같아서 적 줄에 둔다)
    //       "Rounds": [                    ← 그 적의 라운드 목록
    //         { "Round": 1, ... },         ← 라운드 한 줄                → RoundDef
    //         ...
    //       ]
    //     },
    //     ...
    //   ]

    // [DTO] 적 한 명의 거울. 파일에 적힌 그대로만 담는다.
    public class EnemyDTO
    {
        public Difficulty Difficulty;
        public string PrefabKey;   // "Enemies/SM_Chr_Cowboy_01" ← 아직은 글자(주소)일 뿐
        public string Name;        // "풋내기 조니"
        public int BaseScore;
        public List<RoundDef> Rounds = new List<RoundDef>();
    }

    // [Def] 적 한 명의 런타임 정의. PrefabKey(글자)가 Prefab(실제 에셋)으로 바뀐 완성형.
    [Serializable]
    public class EnemyDef
    {
        public Difficulty Difficulty;
        public GameObject Prefab;        // 실제 프리팹 참조!
        public string Name;              // 수배지(결과판)에 적는 현상범 이름
        public int BaseScore;            // 이기면 받는 기본 점수 (기획서 7장 공식의 1000 자리. 난이도마다 다르다)
        public List<RoundDef> Rounds;    // [0] = 1라운드, [1] = 2라운드 ...

        public int RoundCount => Rounds.Count;   // 마지막 라운드인지 확인할 때 쓴다

        // 라운드 번호(1부터)로 꺼낸다. 리스트 칸 번호는 0부터라서 1을 뺀다.
        public RoundDef GetRound(int round) => Rounds[round - 1];
    }

    // [Def] 라운드 한 줄.
    // DTO 와 Def 를 나누지 않았다. 숫자뿐이라 해석할 것이 없고, 파일에 적힌 값이 곧 쓰는 값이기 때문이다.
    // (교안 TileDef 와 같은 이유 : 키를 실제 에셋으로 바꿀 때만 DTO 와 Def 를 나눈다)
    [Serializable]
    public class RoundDef
    {
        public int Round;        // 라운드 번호 (1 ~ 5)
        public float Distance;   // 적과의 거리 (m). 텔레포트 앵커 위치와 맞아야 해서 난이도끼리 같은 값을 쓴다
        public float FireTime;   // 신호 후 적이 쏘기까지 걸리는 시간 (초). 실제로는 여기에 ±0.08초를 더한다
        public float HitRate;    // 적 명중률 (0 ~ 1). 0.35 = 35%
    }
}

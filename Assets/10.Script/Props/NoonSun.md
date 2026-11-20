# NoonSun (정오의 해)

기획서 5장 : 대치 동안 해가 정점을 향해 돌고 그림자가 짧아진다 → 신호 순간 해는 정수리 위, 그림자는 발밑

교안 SunCycle 과 같은 방식으로 Directional Light 를 돌린다.

- Directional Light 는 회전만 의미가 있다. (위치는 상관없다)
- X축 각도 = 해의 높이. 0 이면 지평선, 90 이면 바로 머리 위
- Y축 각도(yaw) = 해가 있는 방향. 올리는 동안 그대로 둔다

## 언제 움직이나

| 때 | 받는 방송 | 해 |
| :---- | :---- | :---- |
| 1라운드 대치 시작 | RoundStarted(1) | 씬에 놓인 각도 → 정오 각도로, 신호 시각까지 같은 빠르기로 올린다 |
| 정오 신호 | DrawSignaled | 무조건 정오 각도 (마지막 프레임에 조금 모자랐어도 딱 맞춘다) |
| 2라운드부터 | RoundStarted(2~) | 아무것도 안 한다 (이미 정오) |

해는 대치 내내 올라가다가 종이 울리는 순간 정확히 머리 위에서 멈춘다.

1라운드 대치 시간은 매번 다르다 (StageManager 의 First Signal Delay, 5~8초).
그래서 StageManager 는 RoundStarted 를 방송하기 전에 신호 시각(SignalAt)을 먼저 정해 두고,
NoonSun 은 방송을 받자마자 SignalAt 을 읽어 "신호까지 몇 초 동안 올릴지" 를 정한다.

다시 결투는 씬을 다시 열기 때문에 해도 저절로 아침 각도로 돌아간다.

## 사용법

1. 해로 쓸 Directional Light 에 이 스크립트를 붙인다. (Town 씬 : Props/Enviroment/Directional Light)
2. 그 조명의 Rotation X 를 아침 해 높이로 둔다. (작을수록 시작 그림자가 길다)
   Rotation Y 는 해가 있는 방향이다. 그림자는 해 반대쪽으로 생긴다.
3. Noon Angle (정오 각도) 은 기본 90. 정오에 적이 떠 보이면 85~88 로 낮춰 본다.
   (90 이면 그림자가 몸 바로 밑에 숨어서, 발과 땅이 맞닿는 그림자가 잘 안 보인다)
4. 다른 Directional Light 는 끈다. URP 는 가장 밝은 해 하나로만 그림자를 만들고, 나머지는 그림자 자리를 밝혀서 그림자를 옅게 만든다.
5. Lighting 창 → Environment → Sun Source 에 이 조명을 넣는다. 기본 하늘(Skybox)의 해도 같이 움직인다.

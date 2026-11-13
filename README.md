<div align="center">
  <h1>♟️ 장기 아케이드 (Janggi Arcade)</h1>
  <p><strong>전통 장기의 7종 기물 행마법과 모바일 스와이프 조작을 결합한 하이퍼 캐주얼 아케이드 게임 (원스토어 정식 출시)</strong></p>

  <!-- 방패 뱃지들 -->
  <img src="https://img.shields.io/badge/Unity-100000?style=for-the-badge&logo=unity&logoColor=white" alt="Unity">
  <img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white" alt="C#">
  <img src="https://img.shields.io/badge/ONE_store-출시_완료-FF0055?style=for-the-badge" alt="원스토어 출시 완료">
  <img src="https://img.shields.io/badge/Mobile-Android-3DDC84?style=for-the-badge&logo=android&logoColor=white" alt="Android">
  <br><br>
</div>

## 📌 Project Overview
- **개발 기간:** 2026.01 (약 1개월)
- **개발 인원:** 1인 개발 (기획, 시스템 아키텍처, UI/UX, 프로그래밍, 원스토어 출시 전담)
- **출시 플랫폼:** ONE store (원스토어 정식 출시)
- **사용 기술:** Unity, C#, Unity New Input System, PlayerPrefs
- **장르:** 캐주얼 전략 아케이드 퍼즐

---

## 🎮 Game Concept
한국 전통 보드게임 '장기'의 깊이 있는 행마 규칙을 모바일 환경에 맞춰 직관적인 한 손 스와이프 조작으로 풀어낸 캐주얼 아케이드 게임입니다. 
플레이어는 시시각각 몰려오는 적 기물들을 피하거나 잡으며, 기물별 고유 이동 경로(차·포·마·상·사·졸·궁)를 순간적으로 판단하여 최고 점수와 콤보를 달성해야 합니다.

### 💡 주요 특징 (Key Highlights)
1. **7종 장기 기물(차·포·마·상·사·졸·궁)의 정통 행마법 구현:**
   직진하는 '차', 다른 기물을 넘어야 하는 '포', 멱(Obstacle)이 존재하면 길이 막히는 '마'와 '상'의 특수 이동 제약 등 전통 장기의 핵심 행마 규칙을 100% 충실하게 객체 지향 알고리즘으로 모델링했습니다.
2. **반응 속도 최적화 4방향 모바일 스와이프 제스처:**
   Unity New Input System과 터치 델타 벡터 연산을 결합하여 미세한 터치 튐을 방지하고, 0.05초 단위의 민첩한 4방향 스와이프 입력 피드백을 제공합니다.
3. **다이내믹 난이도 조절(DDA) 및 기물 스폰 파이프라인:**
   스코어와 생존 시간에 따라 맵에 등장하는 적 기물의 밀도와 이동 주기가 지수적으로 상승하며, 콤보 연쇄 처치를 통한 폭발적인 스코어링 재미를 선사합니다.
4. **원스토어(ONE store) 정식 런칭 및 최적화:**
   안드로이드 저사양 기기에서도 안정적인 60FPS를 유지하도록 드로우콜(Draw Call) 및 텍스처 아틀라스를 최적화하고, 스토어 심사 기준을 준수하여 정식 상용화 배포를 완료했습니다.

---

## 🛠 Tech Stack & Architecture

### **Core Stack**
- **Engine:** Unity
- **Language:** C#
- **Input:** Unity New Input System (Touch Swipe & Keyboard)
- **Platform SDK:** ONE store Target Android SDK

### **System Architecture Flow**
1. **터치 입력 감지 (`Touch / Swipe Delta`):** 화면 터치 좌표와 스와이프 델타값을 분석해 4방향 이동 의도 추출
2. **행마 유효성 검증 (`Janggi Rule Validator`):** 현재 기물의 종류와 멱(장애물) 위치를 체크해 이동 가능 여부 판독
3. **타일 이동 & 캡처 연산 (`Grid Board Manager`):** 적 기물 충돌 시 캡처 처리 및 파티클·사운드 피드백 동시 트리거
4. **콤보 & 점수 정산 (`Score / Combo System`):** 연속 처치 타이머 갱신 및 가산 배율을 적용한 실시간 스코어 반영
5. **게임 오버 & 랭킹 갱신 (`PlayerPrefs / SaveData`):** 궁(King) 피격 시 게임 종료 및 최고 기록 로컬 영구 보관

---

## 🚀 Getting Started

### Prerequisites
- Unity 2022.3 LTS 이상
- Android 빌드 서포트 패키지

### Installation & Run
```bash
# 1. 저장소 클론
git clone https://github.com/LJS0817/TossGame_Janggi.git

# 2. Unity Hub에서 프로젝트 오픈 및 MainScene 실행
```

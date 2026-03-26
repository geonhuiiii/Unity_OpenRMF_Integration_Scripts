# Git 사용 가이드

이 리포지토리(`Unity_OpenRMF_Integration_Scripts`)의 변경사항을 GitHub에 올리는 방법입니다.

---

## 1. 수정한 파일 확인

```bash
git status
```

`modified:` 로 표시된 파일이 수정된 파일입니다.

---

## 2. 변경 내용 확인 (선택)

```bash
# 전체 변경 내용 보기
git diff

# 특정 파일만 보기
git diff Editor/BuildingYamlGenerator.cs
```

---

## 3. 파일 스테이징 (올릴 파일 선택)

```bash
# 특정 파일만 추가
git add Editor/BuildingYamlGenerator.cs

# 수정된 파일 전부 추가
git add .
```

---

## 4. 커밋 (변경 기록 저장)

```bash
git commit -m "변경 내용을 설명하는 메시지"
```

**커밋 메시지 예시:**
- `"Fix coordinate mapping in BuildingYamlGenerator"`
- `"Add wall detection to LiDAR scanner"`
- `"Update PID controller parameters"`

---

## 5. GitHub에 푸시 (업로드)

```bash
git push
```

---

## 전체 과정 한 줄 요약

```bash
git add . ; git commit -m "메시지" ; git push
```

---

## 자주 쓰는 명령어

| 명령어 | 설명 |
|--------|------|
| `git status` | 현재 변경 상태 확인 |
| `git diff` | 수정 내용 상세 보기 |
| `git add .` | 모든 변경 파일 스테이징 |
| `git add 파일명` | 특정 파일만 스테이징 |
| `git commit -m "메시지"` | 커밋 생성 |
| `git push` | GitHub에 업로드 |
| `git log --oneline` | 커밋 히스토리 확인 |
| `git restore 파일명` | 수정 취소 (원래대로 되돌리기) |

---

## 주의사항

- `.meta` 파일은 `.gitignore`에 의해 자동 제외됩니다.
- 커밋 메시지는 **무엇을 왜 바꿨는지** 간결하게 적어주세요.
- `git restore 파일명`은 되돌리면 복구 불가이니 신중하게 사용하세요.

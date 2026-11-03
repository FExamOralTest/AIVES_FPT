# AI EVALUATION SYSTEM - TASK BOARD

**Trạng thái**: 🚧 ĐANG THỰC HIỆN — RUBRIC-DRIVEN SCORING V2 (2026-10-29)
**Task hiện tại**: Chấm điểm thật theo rubric (bỏ trọng số cứng 40/30/20/10)
**Dự án**: AIVES AI Evaluation System

---

## 1. [KẾ HOẠCH & KIẾN TRÚC]

### 1.1 Mục tiêu
Điểm số do rubric quyết định: mỗi criterion AI trả `awarded_score`, điểm câu = tổng awarded_score, điểm bài = tổng điểm câu (Backend tự tính). Xoá luồng chấm cũ dùng `scoring_prompt.txt` (40/30/20/10). Lưu audit đầy đủ trong `answers.ai_analysis`, API review trả về, UI giáo viên hiển thị bảng rubric, sinh viên chỉ thấy sau khi publish.

### 1.2 Quy tắc bất biến (chốt — cả AI service và Backend phải enforce)
1. `0 <= awarded_score <= criterion.max_score` (sai số 1e-4).
2. Judgment phải khớp điểm:
   - `met` ⇒ `awarded_score == max_score`
   - `not_met` ⇒ `awarded_score == 0`
   - `partial` ⇒ `0 < awarded_score < max_score`
3. `question.score = Σ awarded_score` của các criterion (AI service tính; Backend tính lại và dùng giá trị của mình).
4. `question.max_score = Σ criterion.max_score` = rubric snapshot; nếu lệch `request.max_score` ⇒ reject.
5. Có **đúng một** analysis cho mỗi criterion id trong rubric; không thừa, không thiếu, không đổi id.
6. `evidence_quotes` phải là chuỗi con của transcript (sau normalize); quote rỗng/không tồn tại ⇒ reject.
7. `session.total_score = Σ question.score`; `session.max_total_score = Σ question.max_score`; `percentage = total/max*100`. **Backend tự tính**, bỏ qua giá trị AI trả nếu lệch (chỉ log warning, không reject).
8. `schema_version = "2.0"` cho audit.

### 1.3 Thiết kế audit cho mỗi Answer (`answers.ai_analysis` JSONB)
```json
{
  "schemaVersion": "2.0",
  "provider": "gemini",
  "model": "gemini-3.1-flash-lite",
  "gradedAt": "2026-10-29T09:00:00Z",
  "transcript": "…",
  "rubric": [{ "criterionId": "12", "description": "…", "maxScore": 4.0 }],
  "evaluation": {
    "questionId": "…", "questionText": "…", "studentAnswer": "…",
    "score": 6.5, "maxScore": 10.0, "feedback": "…",
    "strengths": [], "weaknesses": [], "missingContent": [],
    "sentenceFeedback": [{ "sentence": "…", "judgment": "correct", "comment": "…" }],
    "criterionAnalyses": [{
      "criterionId": "12", "awardedScore": 2.5, "judgment": "partial",
      "explanation": "…", "errors": [], "omissions": ["…"], "evidenceQuotes": ["…"]
    }],
    "improvements": []
  }
}
```
Không lưu prompt, không lưu sample answer trong payload trả cho sinh viên.

### 1.4 Danh sách file thay đổi

**AI service (`E:\AIVES_FPT\ai-service`)**
| File | Việc |
|---|---|
| `app/models/feedback.py` | Thêm `awarded_score: float (ge=0)` vào `CriterionAnalysis` |
| `app/models/response.py` | `EvaluateAnswerResponse` + `EvaluateSessionResponse`: thêm `provider`, `model`, `schema_version` |
| `app/models/ai_output.py` | Xoá `ScoringOutput` (chỉ dùng cho luồng cũ) |
| `app/services/answer_evidence.py` | `blank_analysis` → awarded 0; `build_mock_analysis` → met=max, partial=ratio*max (4dp), not_met=0; `validate_analysis` → enforce mục 1.2 (1,2,3,4,5,6) |
| `app/services/feedback_analysis_service.py` | Truyền `max_score` của câu vào validate; bỏ phụ thuộc scoring |
| `app/services/evaluation_service.py` | **Bỏ `ScoringService`**; dựng response hoàn toàn từ analysis; `score = Σ awarded`; session tự cộng tổng; thêm provider/model/schema_version |
| `app/services/scoring_service.py` | **XOÁ** (luồng trọng số cứng) |
| `app/prompts/scoring_prompt.txt` | **XOÁ** |
| `app/prompts/feedback_analysis_prompt.txt` | Thêm luật `awarded_score`, bỏ câu "Do not award points"; schema JSON output có `awarded_score` |
| `app/core/ai_client.py` (hoặc `config.py`) | Helper `get_model_name()` trả model đang dùng (`gemini_model`/`openai_model`/`mock`) |
| `app/models/contracts.py`, `app/services/response_validator.py` | Gỡ `AIEvaluationContract`/`RubricResultContract`/`FeedbackContract` + `validate_evaluation` (dead code); **giữ** `QuestionGenerationContract`, `FollowUpQuestionContract` vì `followup_service` còn dùng |
| `tests/conftest.py` | Xoá fixture legacy `rubricResults`, `mock_ai_response_*` chỉ phục vụ contract cũ; thêm fixture rubric + fake client trả awarded_score |
| `tests/test_evaluation_edge_cases.py` | Viết lại theo luồng mới (bỏ test ScoringService) |
| `tests/test_contracts.py` | Bỏ test `AIEvaluationContract`; giữ test question/follow-up |
| `tests/test_response_validator.py` | Bỏ test `AIEvaluationContract` |
| `tests/test_rubric_scoring.py` | **MỚI**: tổng criterion = điểm câu; rubric khác ⇒ điểm khác; reject vượt max/thiếu criterion/quote ngoài transcript/judgment lệch điểm; transcript rỗng ⇒ 0 |
| `tests/integration/test_rubric_scoring_live.py` | **MỚI** (marker `integration`): Gemini thật trả `awarded_score`, tổng = điểm câu, quote có trong transcript |

**Backend (`E:\AIVES_FPT\backend`)**
| File | Việc |
|---|---|
| `Aives.Application/AI/DTOs/AiIntegrationDtos.cs` | `CriterionAnalysisDto` + `awarded_score` (`[JsonRequired]`); `EvaluateSessionResponse`/`EvaluateAnswerResponse` + `provider`, `model`, `schema_version`; `StoredAnswerAnalysisDto` + audit fields (`SchemaVersion`, `Provider`, `Model`, `GradedAt`, `Transcript`, `Rubric`) |
| `Aives.Application/AI/EvaluationContract.cs` | Validate lại toàn bộ mục 1.2; **tính lại** `score` từng câu và totals của session (không tin AI) |
| `Aives.Application/Viva/Services/VivaSessionService.cs` | Ghi audit JSON đầy đủ vào `answer.AiAnalysis`; dùng totals backend tính; `graded_at = DateTime.UtcNow` |
| `Aives.Application/Exams/Services/ResultService.cs` | Teacher: trả `aiAnalysis` (đã có). Student: chỉ trả breakdown khi `PUBLISHED`, DTO lọc bỏ `sampleAnswer`/`context` |
| `Aives.Application/Exams/DTOs/*` | `StudentAnswerItemDto` + `AiAnalysis` (bản lọc) |
| `Aives.Tests/**` | Test contract: awarded vượt max, thiếu criterion, quote ngoài transcript, judgment lệch điểm, totals bị AI làm sai ⇒ backend sửa lại |

**Frontend (`E:\AIVES_FPT\frontend\aives-web`)**
| File | Việc |
|---|---|
| `src/features/teacher-results/types.ts` | Khai báo `aiAnalysis` (schemaVersion, provider, model, gradedAt, transcript, rubric, evaluation.criterionAnalyses[].awardedScore…) |
| `src/features/teacher-results/components/ResultReviewModal.tsx` | Bảng rubric mỗi answer: tiêu chí, `điểm đạt / max`, judgment, evidence quotes, giải thích |
| `src/features/student-results/types.ts` + page | Breakdown chỉ khi `isPublished`; không hiện sample answer |
| — | `npm run build` + `npm run lint` phải pass |

**Docs**
| File | Việc |
|---|---|
| `docs/api/api-contract.md` | §8/§9: thêm `awarded_score`, `provider`, `model`, `schema_version`; nêu rõ điểm do rubric quyết định và backend tự tính tổng |
| `docs/ai/*` (nếu mô tả chấm điểm) | Cập nhật công thức điểm |

### 1.5 Thứ tự thực thi
1. AI service: model + prompt + service + tests → `pytest -q` xanh.
2. Backend: DTO + contract + audit + test → `dotnet build` + `dotnet test` xanh.
3. Frontend: type + UI → `npm run build` xanh.
4. Docs + dọn legacy.
5. E2E: Postgres (docker) + backend (dotnet run) + ai-service (venv, Gemini thật) + frontend (vite) → submit 1 bài → kiểm tra DB/API/UI.

### 1.6 Bằng chứng bắt buộc để PASS
- [x] `pytest -q` toàn bộ xanh; rubric live Gemini integration xanh (114 passed; 2 live tests passed).
- [ ] `dotnet test` xanh.
- [x] `npm run build` và `npm run lint` xanh.
- [x] Gemini trả `awarded_score` từng tiêu chí; tổng = điểm câu (live rubric integration).
- [ ] DB `answers.ai_analysis` có `schemaVersion/provider/model/gradedAt/transcript/rubric/evaluation.criterionAnalyses[].awardedScore`.
- [ ] `GET` API review trả `aiAnalysis` cho từng answer.
- [ ] UI giáo viên hiển thị bảng rubric (Playwright chụp/assert).
- [x] Cùng transcript + rubric khác nhau ⇒ điểm khác nhau (unit + live integration tests).

---

## 2. [NHẬT KÝ CODE & DEV SERVER] (Do Coder cập nhật)

- AI service: bỏ scoring trọng số cố định; criterion `awarded_score` là nguồn điểm, có validate schema v2 và provider/model audit.
- Backend: enforce rubric/criterion/evidence contracts, tính lại điểm/tổng, lưu audit v2 và chỉ trả breakdown cho student khi published.
- Frontend: teacher/student có bảng breakdown rubric; cập nhật API docs và README.
- Kiểm thử: `pytest -q` 114 passed; rubric Gemini integration 2 passed; `npm run build` + `npm run lint` passed; `dotnet test` (lọc Cloudflare R2) 260 passed, 1 skipped.
- E2E Postgres/API/UI chưa chạy: Docker daemon hiện không hoạt động; Cloudflare R2 integration test cần credentials không có trong cấu hình local.

---

## 3. [BÁO CÁO KIỂM THỬ TRÌNH DUYỆT] (Do QA Tester cập nhật)

Chưa chạy Playwright/browser E2E vì Docker daemon không khởi động được trong môi trường hiện tại; UI đã qua production build và lint.

---

## 4. [LƯU TRỮ — BOARD CŨ 2026-10-20]

Các mục dưới đây là lịch sử giai đoạn 1–2 (contract cũ, trọng số 40/30/20/10). Không còn hiệu lực sau task rubric v2.

### Kết quả cũ (tham khảo)
- Contract cũ: `AIEvaluationContract` với `rubricResults[].score/maxScore` — **đã bị thay thế** bởi `criterion_analyses[].awarded_score`.
- Luồng chấm cũ: `scoring_service.py` + `scoring_prompt.txt` trọng số 40/30/20/10 — **đã xoá**.
- Test suite cũ: 110 passed (trước task này).

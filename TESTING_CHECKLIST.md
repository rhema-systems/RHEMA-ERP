# 🧪 Testing Checklist - Tender Features

## ✅ Code Review Status

### 1. Notification URL Fix
- ✅ Backend notification service updated
- ✅ In-app notification ActionUrl: `/external-portal/tenders/{tenderId}`
- ✅ Email template link: `/external-portal/tenders/{tender.Id}`
- ✅ No compilation errors

### 2. Tender Clarifications
- ✅ Backend GET endpoint added: `GET /api/procurement/Tenders/{id}/clarifications`
- ✅ Frontend service methods added: `getTenderClarifications()`, `createClarification()`
- ✅ Component created: `TenderClarifications.tsx` (External Portal)
- ✅ Component created: `AnswerClarificationDialog.tsx` (Internal Portal)
- ✅ External portal tab added with ask question form
- ✅ Internal portal tab updated with answer button
- ✅ All UI components exist (Textarea, Card, Badge, Dialog, etc.)
- ✅ No TypeScript errors

### 3. Tender Award
- ✅ Component created: `TenderAward.tsx`
- ✅ Internal portal tab added
- ✅ Service methods exist: `generateAwardRecommendation()`, `createAward()`
- ✅ All UI components exist
- ✅ No TypeScript errors

---

## 🧪 Manual Testing Guide

### Test 1: Notification URL Fix

**Prerequisites:**
- Backend running on http://localhost:5000
- Frontend running on http://localhost:3000
- At least one published tender with invited suppliers

**Steps:**
1. Login as internal user (procurement staff)
2. Navigate to a tender and publish it
3. Login as external user (supplier) in a different browser/incognito
4. Check notifications (bell icon)
5. Click on the tender invitation notification
6. **Expected:** Should navigate to `/external-portal/tenders/{id}` without 404 error
7. **Expected:** Tender details page should load correctly

**Status:** ⏳ Pending Manual Test

---

### Test 2: Tender Clarifications (External Portal)

**Prerequisites:**
- Published tender with submission deadline in the future
- External user (supplier) logged in

**Steps:**
1. Navigate to "Available Tenders" in external portal
2. Click on a published tender
3. Click on "Q&A" tab
4. **Expected:** See "Ask a Question" form
5. Enter a question: "What is the delivery timeline for this tender?"
6. Click "Submit Question"
7. **Expected:** Success toast message
8. **Expected:** Question appears in the list with "Pending" badge
9. Refresh the page
10. **Expected:** Question persists

**Status:** ⏳ Pending Manual Test

---

### Test 3: Answer Clarifications (Internal Portal)

**Prerequisites:**
- At least one clarification question submitted
- Internal user (procurement staff) logged in

**Steps:**
1. Navigate to "Procurement" > "Tenders"
2. Click on the tender with clarifications
3. Click on "Clarifications" tab
4. **Expected:** See list of clarifications with "Pending" status
5. **Expected:** See "Answer Question" button on pending clarifications
6. Click "Answer Question" button
7. **Expected:** Answer dialog opens
8. Enter answer: "The delivery timeline is 30 days from contract signing"
9. **Expected:** "Make this answer visible to all bidders" checkbox is checked by default
10. Click "Submit Answer"
11. **Expected:** Success toast message
12. **Expected:** Dialog closes
13. **Expected:** Clarification shows "Answered" badge
14. Login as external user
15. Navigate to the same tender's Q&A tab
16. **Expected:** See the answer displayed in green box

**Status:** ⏳ Pending Manual Test

---

### Test 4: Tender Award Recommendation

**Prerequisites:**
- Tender with status "Closed"
- At least 2 bids submitted
- Bids have been evaluated with scores
- Internal user (procurement staff) logged in

**Steps:**
1. Navigate to tender details
2. Click on "Award" tab
3. **Expected:** See "Award Recommendation" card
4. **Expected:** See summary cards: Total Bids, Evaluated Bids, Recommended Amount
5. **Expected:** See "Recommended Winner" highlighted in green
6. **Expected:** See "Bid Comparison" table with all bids ranked by score
7. **Expected:** Top bid has trophy icon
8. **Expected:** Each bid shows: Business Partner, Bid Number, Amount, Score, Evaluations

**Status:** ⏳ Pending Manual Test

---

### Test 5: Award Tender

**Prerequisites:**
- Award recommendation loaded successfully
- Internal user with award permissions

**Steps:**
1. In the "Award" tab, click "Award" button on a bid
2. **Expected:** Award confirmation dialog opens
3. **Expected:** Dialog shows tender details
4. **Expected:** Award amount is pre-filled with bid amount
5. Modify award amount if needed
6. Enter justification: "Selected based on best value for money and highest evaluation score"
7. Click "Confirm Award"
8. **Expected:** Success toast message
9. **Expected:** Dialog closes
10. **Expected:** Award tab now shows "Tender Awarded" card
11. **Expected:** Shows awarded business partner, amount, date, and justification
12. Check tender status
13. **Expected:** Tender status changed to "Awarded"

**Status:** ⏳ Pending Manual Test

---

## 🐛 Known Issues to Check

1. **Award Notifications:** Verify that notifications are sent to all bidders after awarding.
2. **Permissions:** Verify that only authorized users can award tenders.
3. **Validation:** Test edge cases like awarding with empty justification, negative amounts, etc.
4. **Clarification Properties:** Backend uses `isAnswered` property but DTO might use `answer != null` check. Verify consistency.

---

## 📊 Test Results Summary

| Feature | Code Review | Manual Test | Status |
|---------|-------------|-------------|--------|
| Notification URL Fix | ✅ Pass | ⏳ Pending | - |
| Clarifications (Ask) | ✅ Pass | ⏳ Pending | - |
| Clarifications (Answer) | ✅ Pass | ⏳ Pending | - |
| Award Recommendation | ✅ Pass | ⏳ Pending | - |
| Award Creation | ✅ Pass | ⏳ Pending | - |

---

## 🔧 Additional Improvements Needed

1. ✅ ~~Answer Clarifications UI~~ - **COMPLETED**
2. **Real-time Updates:** Consider adding SignalR for real-time clarification updates
3. **Email Notifications:** Verify clarification and award email notifications are sent
4. **Audit Trail:** Verify all actions are logged properly
5. **Bulk Answer:** Consider adding ability to answer multiple clarifications at once


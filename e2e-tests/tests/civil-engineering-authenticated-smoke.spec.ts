import { expect, test, type Browser, type BrowserContext, type Page } from '@playwright/test';

const required = (name: string): string => {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} must be configured for Civil Engineering acceptance.`);
  return value;
};

type ActorSession = {
  context: BrowserContext;
  page: Page;
  token: string;
};

const authorization = (session: ActorSession) => ({ Authorization: `Bearer ${session.token}` });

async function signIn(
  browser: Browser,
  username: string,
  password: string,
  browserErrors: string[],
): Promise<ActorSession> {
  const baseURL = process.env.E2E_BASE_URL?.trim() || 'http://127.0.0.1:3000';
  const api = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5000';
  const tenantCode = process.env.CIVIL_ACCEPTANCE_TENANT_CODE?.trim() || 'DEFAULT';
  const context = await browser.newContext({ baseURL });
  const page = await context.newPage();
  page.on('pageerror', error => browserErrors.push(`pageerror: ${error.message}`));
  page.on('response', response => {
    if (response.status() >= 500) {
      browserErrors.push(`response: ${response.status()} ${response.request().method()} ${response.url()}`);
    }
  });

  const response = await context.request.post(`${api}/api/auth/login`, {
    data: { username, password, tenantCode, rememberMe: false },
  });
  expect(response.status(), `Civil acceptance login failed for actor ${username}.`).toBe(200);
  const login = (await response.json()) as { token?: string; refreshToken?: string };
  expect(login.token, `Civil acceptance login returned no bearer token for actor ${username}.`).toBeTruthy();

  await page.goto('/login');
  await page.evaluate(
    ({ token, refreshToken }) => {
      localStorage.setItem('authToken', token);
      localStorage.setItem('token', token);
      if (refreshToken) localStorage.setItem('refreshToken', refreshToken);
    },
    { token: login.token!, refreshToken: login.refreshToken },
  );

  return { context, page, token: login.token! };
}

async function selectOption(page: Page, testId: string, optionName: string): Promise<void> {
  const trigger = page.getByTestId(testId);
  await expect(trigger).toBeVisible({ timeout: 60_000 });
  await trigger.click();
  const option = page.getByRole('option', { name: optionName, exact: true });
  await expect(option).toBeVisible({ timeout: 60_000 });
  await option.click();
}

async function openTaskBoard(page: Page, projectLabel: string): Promise<void> {
  await page.goto('/development/civil-engineering/direct-tasks');
  await expect(page.getByRole('heading', { name: 'Civil task assignments', exact: true })).toBeVisible({ timeout: 60_000 });
  await expect(page.getByTestId('civil-direct-tasks-page')).toBeVisible();
  await selectOption(page, 'civil-task-project-select', projectLabel);
}

test('Civil direct task is visibly assigned, completed and independently accepted with permission denial', async ({
  browser,
  request,
}) => {
  test.skip(
    process.env.CIVIL_ACCEPTANCE_ENABLED !== '1',
    'Set CIVIL_ACCEPTANCE_ENABLED=1 and provide the disposable four-actor Civil seed environment.',
  );
  test.setTimeout(8 * 60_000);

  const api = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5000';
  const projectId = required('CIVIL_ACCEPTANCE_PROJECT_ID');
  const projectLabel = required('CIVIL_ACCEPTANCE_PROJECT_LABEL');
  const assigneeLabel = required('CIVIL_ACCEPTANCE_ASSIGNEE_LABEL');
  const actorNames = [
    required('CIVIL_ACCEPTANCE_ASSIGNER_USERNAME'),
    required('CIVIL_ACCEPTANCE_ASSIGNEE_USERNAME'),
    required('CIVIL_ACCEPTANCE_REVIEWER_USERNAME'),
    required('CIVIL_ACCEPTANCE_UNAUTHORIZED_USERNAME'),
  ];
  expect(new Set(actorNames).size, 'Civil maker, assignee, reviewer and unauthorized actor must be distinct.').toBe(4);

  const browserErrors: string[] = [];
  const sessions: ActorSession[] = [];
  const title = required('CIVIL_ACCEPTANCE_TASK_TITLE');

  try {
    const assigner = await signIn(
      browser,
      actorNames[0],
      required('CIVIL_ACCEPTANCE_ASSIGNER_PASSWORD'),
      browserErrors,
    );
    sessions.push(assigner);

    await openTaskBoard(assigner.page, projectLabel);
    await expect(assigner.page.getByText('Assign Civil work', { exact: true })).toBeVisible();
    await selectOption(assigner.page, 'civil-task-assignee-select', assigneeLabel);
    await selectOption(assigner.page, 'civil-task-urgency-select', 'Routine');
    const tomorrow = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
    await assigner.page.getByTestId('civil-task-due-date').fill(tomorrow);
    await assigner.page.getByTestId('civil-task-title').fill(title);
    await assigner.page.getByTestId('civil-task-instructions').fill(
      'Inspect the controlled test location and submit a concise completion result for independent review.',
    );
    await assigner.page.getByTestId('civil-task-assign').click();
    await expect(assigner.page.getByText(title, { exact: true })).toBeVisible({ timeout: 60_000 });

    const assignedResponse = await assigner.context.request.get(
      `${api}/api/projects/${projectId}/civil-engineering/direct-tasks`,
      { headers: authorization(assigner) },
    );
    expect(assignedResponse.status()).toBe(200);
    const assignedTasks = (await assignedResponse.json()) as Array<{ id: string; title: string; status: string }>;
    const assignedTask = assignedTasks.find(item => item.title === title);
    expect(assignedTask).toMatchObject({ title, status: 'Assigned' });

    const unauthenticated = await request.get(
      `${api}/api/projects/${projectId}/civil-engineering/direct-tasks`,
    );
    expect(unauthenticated.status()).toBe(401);

    const unauthorized = await signIn(
      browser,
      actorNames[3],
      required('CIVIL_ACCEPTANCE_UNAUTHORIZED_PASSWORD'),
      browserErrors,
    );
    sessions.push(unauthorized);
    await unauthorized.page.goto('/development/civil-engineering/direct-tasks');
    await expect(unauthorized.page.getByText('Civil assignment access required', { exact: true })).toBeVisible({ timeout: 30_000 });
    await expect(unauthorized.page.getByTestId('civil-task-assign')).toHaveCount(0);
    const forbidden = await unauthorized.context.request.get(
      `${api}/api/projects/${projectId}/civil-engineering/direct-tasks/lookups`,
      { headers: authorization(unauthorized) },
    );
    expect(forbidden.status()).toBe(403);

    const assignee = await signIn(
      browser,
      actorNames[1],
      required('CIVIL_ACCEPTANCE_ASSIGNEE_PASSWORD'),
      browserErrors,
    );
    sessions.push(assignee);
    await openTaskBoard(assignee.page, projectLabel);
    let taskCard = assignee.page.locator('article').filter({ hasText: title });
    await expect(taskCard).toHaveCount(1);
    await taskCard.getByRole('button', { name: 'Feedback', exact: true }).click();
    await selectOption(assignee.page, 'civil-task-feedback-action', 'Acknowledge');
    await assignee.page.getByTestId('civil-task-save-feedback').click();
    await expect(taskCard.getByText('InProgress', { exact: true })).toBeVisible({ timeout: 30_000 });
    const acknowledgedResponse = await assignee.context.request.get(
      `${api}/api/projects/${projectId}/civil-engineering/direct-tasks`,
      { headers: authorization(assignee) },
    );
    expect(acknowledgedResponse.status()).toBe(200);
    expect(((await acknowledgedResponse.json()) as Array<{ id: string; status: string }>).find(item => item.id === assignedTask!.id))
      .toMatchObject({ status: 'InProgress' });

    await taskCard.getByRole('button', { name: 'Close feedback', exact: true }).click();
    await taskCard.getByRole('button', { name: 'Feedback', exact: true }).click();
    await selectOption(assignee.page, 'civil-task-feedback-action', 'Complete');
    await assignee.page.getByTestId('civil-task-feedback-message').fill(
      'The controlled test inspection is complete and ready for independent acceptance.',
    );
    await assignee.page.getByTestId('civil-task-save-feedback').click();
    await expect(taskCard.getByText('PendingAcceptance', { exact: true })).toBeVisible({ timeout: 45_000 });
    const completedResponse = await assignee.context.request.get(
      `${api}/api/projects/${projectId}/civil-engineering/direct-tasks`,
      { headers: authorization(assignee) },
    );
    expect(completedResponse.status()).toBe(200);
    expect(((await completedResponse.json()) as Array<{ id: string; status: string }>).find(item => item.id === assignedTask!.id))
      .toMatchObject({ status: 'PendingAcceptance' });

    const reviewer = await signIn(
      browser,
      actorNames[2],
      required('CIVIL_ACCEPTANCE_REVIEWER_PASSWORD'),
      browserErrors,
    );
    sessions.push(reviewer);
    await openTaskBoard(reviewer.page, projectLabel);
    taskCard = reviewer.page.locator('article').filter({ hasText: title });
    await expect(taskCard).toHaveCount(1);
    await taskCard.getByRole('button', { name: 'Feedback', exact: true }).click();
    await selectOption(reviewer.page, 'civil-task-feedback-action', 'Accept');
    await reviewer.page.getByTestId('civil-task-feedback-message').fill(
      'Independent Civil review confirms the submitted task outcome.',
    );
    await reviewer.page.getByTestId('civil-task-save-feedback').click();
    await expect(taskCard.getByText('Accepted', { exact: true })).toBeVisible({ timeout: 45_000 });

    const finalResponse = await assigner.context.request.get(
      `${api}/api/projects/${projectId}/civil-engineering/direct-tasks`,
      { headers: authorization(assigner) },
    );
    expect(finalResponse.status()).toBe(200);
    const finalTasks = (await finalResponse.json()) as Array<{
      id: string;
      title: string;
      status: string;
      progressPercent: number;
      acceptedAt?: string;
    }>;
    expect(finalTasks.find(item => item.id === assignedTask!.id)).toMatchObject({
      title,
      status: 'Accepted',
      progressPercent: 100,
    });
    expect(finalTasks.find(item => item.id === assignedTask!.id)?.acceptedAt).toBeTruthy();

    await assigner.page.reload();
    await selectOption(assigner.page, 'civil-task-project-select', projectLabel);
    const finalCard = assigner.page.locator('article').filter({ hasText: title });
    await expect(finalCard).toHaveCount(1);
    await expect(finalCard.getByText('Accepted', { exact: true })).toBeVisible({ timeout: 30_000 });
    expect(browserErrors, browserErrors.join('\n')).toEqual([]);
  } finally {
    await Promise.all(sessions.map(session => session.context.close()));
  }
});

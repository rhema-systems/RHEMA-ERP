import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const source = fs.readFileSync(
  path.resolve('..', 'src', 'ErpSystem.Api', 'Services', 'DatabaseSeedingService.cs'),
  'utf8',
);

const passwordFor = (username) => {
  const escaped = username.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = source.match(
    new RegExp(`CreateTestUserAsync\\("${escaped}"\\s*,\\s*"[^"]+"\\s*,\\s*"([^"]+)"`),
  );
  if (!match) throw new Error(`No repository-owned disposable test credential exists for ${username}.`);
  return match[1];
};

const actors = {
  maker: 'employee',
  contractor: 'external',
  consultant: 'helpdesk.agent',
  reviewer: 'helpdesk.supervisor',
  workflowReviewer: 'accounts.officer',
  engineer: 'helpdesk.manager',
  financeValidator: 'finance.manager',
  checker: 'manager',
};

const acceptanceConnection = process.env.ConnectionStrings__DefaultConnection?.trim();
if (!acceptanceConnection) {
  throw new Error(
    'ConnectionStrings__DefaultConnection must explicitly target the disposable QS acceptance database.',
  );
}
const databaseName = acceptanceConnection
  .split(';')
  .map((part) => part.trim())
  .find((part) => /^(?:database|initial catalog)=/i.test(part))
  ?.split('=', 2)[1]
  ?.trim();
if (!databaseName || !/^RhemaQsUatAssurance_[0-9]{8}$/i.test(databaseName)) {
  throw new Error(
    'QS acceptance refused: the database name must match RhemaQsUatAssurance_YYYYMMDD.',
  );
}

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const fixtureDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'tdc-qs-fixture-'));
const fixturePath = path.join(fixtureDirectory, 'fixture.json');

try {
  const seed = spawnSync(
    'powershell.exe',
    [
      '-NoProfile',
      '-ExecutionPolicy',
      'Bypass',
      '-File',
      path.resolve('..', 'scripts', 'quantity-survey', 'Invoke-QuantitySurveyE2ESeed.ps1'),
      '-OutputJsonPath',
      fixturePath,
    ],
    { cwd: process.cwd(), env: process.env, stdio: 'inherit', shell: false },
  );
  if (seed.error) throw seed.error;
  if (seed.status !== 0) {
    throw new Error(`The governed QS fixture failed with exit code ${seed.status ?? 1}.`);
  }

  const fixture = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));
  const requiredFixtureIds = {
    ProjectId: fixture.ProjectId,
    InterimValuationId: fixture.InterimValuationId,
    ApprovedBoqVersionId: fixture.ApprovedBoqVersionId,
    ContractorBusinessPartnerId: fixture.ContractorBusinessPartnerId,
    ConsultantBusinessPartnerId: fixture.ConsultantBusinessPartnerId,
  };
  for (const [name, value] of Object.entries(requiredFixtureIds)) {
    if (typeof value !== 'string' || !guid.test(value)) {
      throw new Error(`The governed QS fixture did not emit a valid ${name}.`);
    }
  }

  const env = {
    ...process.env,
    QS_ACCEPTANCE_INTERACTIVE: '1',
    E2E_API_URL: 'http://127.0.0.1:5200',
    E2E_BASE_URL: 'http://127.0.0.1:3200',
    QS_ACCEPTANCE_PROJECT_ID: fixture.ProjectId,
    QS_ACCEPTANCE_VALUATION_ID: fixture.InterimValuationId,
    QS_ACCEPTANCE_BOQ_VERSION_ID: fixture.ApprovedBoqVersionId,
    QS_ACCEPTANCE_CONTRACTOR_ID: fixture.ContractorBusinessPartnerId,
    QS_ACCEPTANCE_CONSULTANT_ID: fixture.ConsultantBusinessPartnerId,
    QS_ACCEPTANCE_MAKER_USERNAME: actors.maker,
    QS_ACCEPTANCE_MAKER_PASSWORD: passwordFor(actors.maker),
    QS_ACCEPTANCE_CONTRACTOR_USERNAME: actors.contractor,
    QS_ACCEPTANCE_CONTRACTOR_PASSWORD: passwordFor(actors.contractor),
    QS_ACCEPTANCE_CONSULTANT_USERNAME: actors.consultant,
    QS_ACCEPTANCE_CONSULTANT_PASSWORD: passwordFor(actors.consultant),
    QS_ACCEPTANCE_REVIEWER_USERNAME: actors.reviewer,
    QS_ACCEPTANCE_REVIEWER_PASSWORD: passwordFor(actors.reviewer),
    QS_ACCEPTANCE_WORKFLOW_REVIEWER_USERNAME: actors.workflowReviewer,
    QS_ACCEPTANCE_WORKFLOW_REVIEWER_PASSWORD: passwordFor(actors.workflowReviewer),
    QS_ACCEPTANCE_ENGINEER_USERNAME: actors.engineer,
    QS_ACCEPTANCE_ENGINEER_PASSWORD: passwordFor(actors.engineer),
    QS_ACCEPTANCE_FINANCE_VALIDATOR_USERNAME: actors.financeValidator,
    QS_ACCEPTANCE_FINANCE_VALIDATOR_PASSWORD: passwordFor(actors.financeValidator),
    QS_ACCEPTANCE_CHECKER_USERNAME: actors.checker,
    QS_ACCEPTANCE_CHECKER_PASSWORD: passwordFor(actors.checker),
  };

  const result = spawnSync(
    'npx.cmd',
    ['playwright', 'test', 'tests/qs-phases-0-6-lifecycle.spec.ts', '--project=chromium', '--workers=1', '--reporter=line'],
    { cwd: process.cwd(), env, stdio: 'inherit', shell: true },
  );

  if (result.error) console.error(result.error.message);
  if ((result.status ?? 1) !== 0) {
    process.exitCode = result.status ?? 1;
  } else {
    const verification = spawnSync(
      'powershell.exe',
      [
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        path.resolve(
          '..',
          'scripts',
          'quantity-survey',
          'Invoke-QuantitySurveyE2EVerification.ps1',
        ),
      ],
      { cwd: process.cwd(), env: process.env, stdio: 'inherit', shell: false },
    );
    if (verification.error) console.error(verification.error.message);
    process.exitCode = verification.status ?? 1;
  }
} finally {
  fs.rmSync(fixtureDirectory, { recursive: true, force: true });
}

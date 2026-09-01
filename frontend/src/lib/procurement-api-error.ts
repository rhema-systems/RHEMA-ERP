import { getProcurementProblemMessage } from './procurement-tender-header-actions';

export async function throwProcurementResponseError(
  response: Response,
  fallback: string
): Promise<never> {
  let problem: unknown;

  try {
    const text = await response.text();
    if (text) {
      try {
        problem = JSON.parse(text);
      } catch {
        problem = text;
      }
    }
  } catch {
    problem = undefined;
  }

  throw new Error(getProcurementProblemMessage(problem, fallback));
}

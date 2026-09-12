const STEPS = ['Template', 'Upload', 'Review', 'Commit', 'Result'] as const;

/** The five-step strip at the top of every import page. `current` is 1-based. */
export function WizardSteps({ current }: { current: number }) {
  return (
    <ol className="flex flex-wrap items-center gap-2 text-sm">
      {STEPS.map((label, i) => {
        const n = i + 1;
        const state = n < current ? 'done' : n === current ? 'current' : 'todo';
        return (
          <li key={label} className="flex items-center gap-2">
            <span
              className={
                'flex h-6 w-6 items-center justify-center rounded-full border text-xs font-medium ' +
                (state === 'current'
                  ? 'border-primary bg-primary text-primary-foreground'
                  : state === 'done'
                    ? 'border-primary text-primary'
                    : 'border-muted-foreground/40 text-muted-foreground')
              }
            >
              {n}
            </span>
            <span className={state === 'todo' ? 'text-muted-foreground' : ''}>{label}</span>
            {i < STEPS.length - 1 && <span className="mx-1 text-muted-foreground">›</span>}
          </li>
        );
      })}
    </ol>
  );
}

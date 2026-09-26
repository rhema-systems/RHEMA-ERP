import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const sourceRoots = [
    join(process.cwd(), 'src', 'app', 'finance'),
    join(process.cwd(), 'src', 'app', 'administration', 'finance'),
    join(process.cwd(), 'src', 'components', 'finance'),
];

const sourceFiles = (root: string): string[] => readdirSync(root).flatMap((name) => {
    const path = join(root, name);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.(ts|tsx)$/.test(name) && !name.includes('.test.') ? [path] : [];
});

describe('Finance feedback quality', () => {
    it('does not use blocking browser alerts for operational feedback', () => {
        const offenders = sourceRoots.flatMap(sourceFiles)
            .filter((path) => /\b(?:window\.)?alert\s*\(/.test(readFileSync(path, 'utf8')));

        expect(offenders).toEqual([]);
    });

    it('does not use context-free failure fallbacks', () => {
        const offenders = sourceRoots.flatMap(sourceFiles)
            .filter((path) => /['"](?:Failed\.|Something went wrong\.?|An error occurred\.?)['"]/.test(readFileSync(path, 'utf8')));

        expect(offenders).toEqual([]);
    });
});

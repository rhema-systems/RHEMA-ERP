import React from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import ReportTemplates from './ReportTemplates'

const state = vi.hoisted(() => ({
  action: vi.fn(),
  templates: [] as Array<Record<string, unknown>>,
}))

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: vi.fn() }),
  useQuery: ({ queryKey }: { queryKey: string[] }) => ({
    data: queryKey[0] === 'report-templates' ? state.templates : [
      { id: 'report-1', name: 'Procurement spend', status: 'published' },
    ],
    isLoading: false,
  }),
  useMutation: () => ({ mutate: state.action, isPending: false }),
}))

vi.mock('../../hooks/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }))
vi.mock('../../services/reports', () => ({
  reportsService: {
    getReportTemplates: vi.fn(),
    getReportsForAdmin: vi.fn(),
  },
}))

const template = (status: 'Draft' | 'Published' | 'Archived') => ({
  id: 'template-1', reportId: 'report-1', reportName: 'Procurement spend',
  templateKey: 'BOARD-QUARTERLY-SPEND', version: 2, name: 'Board quarterly spend',
  description: 'Board pack', category: 'Procurement', type: 'Table', audience: 'Board',
  cadence: 'Quarterly', status, defaultOutputFormat: 'Online',
  outputFormats: ['Online', 'XLSX', 'PDF'], isCustom: true, createdBy: 'admin',
  createdAt: '2026-08-05T00:00:00Z', usageCount: 0, rowVersion: 'AAAA',
})

describe('ReportTemplates', () => {
  beforeEach(() => {
    state.action.mockReset()
    state.templates = []
  })

  it('shows the configured audience, cadence, formats and published lifecycle actions', () => {
    state.templates = [template('Published')]
    render(<ReportTemplates />)

    expect(screen.getByText('BOARD-QUARTERLY-SPEND · v2 · Procurement spend')).toBeInTheDocument()
    expect(screen.getByText('Board')).toBeInTheDocument()
    expect(screen.getByText('Quarterly')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Generate Online' })).toBeInTheDocument()
    expect(screen.getByLabelText('Clone revision')).toBeInTheDocument()
    expect(screen.getByLabelText('Archive')).toBeInTheDocument()
    expect(screen.queryByLabelText('Edit')).not.toBeInTheDocument()

    fireEvent.click(screen.getByLabelText('Clone revision'))
    expect(state.action).toHaveBeenCalledWith(expect.objectContaining({ action: 'clone' }))
  })

  it('keeps edit, publish and delete controls on Draft revisions only', () => {
    state.templates = [template('Draft')]
    render(<ReportTemplates />)

    expect(screen.getByLabelText('Edit')).toBeInTheDocument()
    expect(screen.getByLabelText('Publish')).toBeInTheDocument()
    expect(screen.getByLabelText('Delete')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Generate/ })).not.toBeInTheDocument()
  })
})

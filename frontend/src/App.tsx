import { BrowserRouter, Routes, Route } from 'react-router-dom'
import Layout from '@/components/Layout'
import HomePage from '@/pages/HomePage'
import StackListPage from '@/pages/StackListPage'
import StackDetailsPage from '@/pages/StackDetailsPage'
import CreateStackWizardPage from '@/pages/CreateStackWizardPage'
import BuildProgressPage from '@/pages/BuildProgressPage'
import NotFoundPage from '@/pages/NotFoundPage'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="stacks" element={<StackListPage />} />
          <Route path="stacks/new" element={<CreateStackWizardPage />} />
          <Route path="stacks/:stackId" element={<StackDetailsPage />} />
          <Route path="stacks/:stackId/build" element={<BuildProgressPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App

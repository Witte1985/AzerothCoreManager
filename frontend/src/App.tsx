import { lazy, Suspense } from 'react'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import Layout from '@/components/Layout'
import PageLoader from '@/components/PageLoader'

const HomePage = lazy(() => import('@/pages/HomePage'))
const StackListPage = lazy(() => import('@/pages/StackListPage'))
const StackDetailsPage = lazy(() => import('@/pages/StackDetailsPage'))
const CreateStackWizardPage = lazy(() => import('@/pages/CreateStackWizardPage'))
const BuildProgressPage = lazy(() => import('@/pages/BuildProgressPage'))
const ContainerLogsPage = lazy(() => import('@/pages/ContainerLogsPage'))
const NotFoundPage = lazy(() => import('@/pages/NotFoundPage'))

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Suspense fallback={<PageLoader />}><HomePage /></Suspense>} />
          <Route path="stacks" element={<Suspense fallback={<PageLoader />}><StackListPage /></Suspense>} />
          <Route path="stacks/new" element={<Suspense fallback={<PageLoader />}><CreateStackWizardPage /></Suspense>} />
          <Route path="stacks/:stackId" element={<Suspense fallback={<PageLoader />}><StackDetailsPage /></Suspense>} />
          <Route path="stacks/:stackId/build" element={<Suspense fallback={<PageLoader />}><BuildProgressPage /></Suspense>} />
          <Route path="stacks/:stackId/containers/:containerName/logs" element={<Suspense fallback={<PageLoader />}><ContainerLogsPage /></Suspense>} />
          <Route path="*" element={<Suspense fallback={<PageLoader />}><NotFoundPage /></Suspense>} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App

import { Outlet, Link } from 'react-router-dom'

export default function Layout() {
  return (
    <div className="min-h-screen bg-gray-50">
      <nav className="bg-white shadow">
        <div className="container mx-auto px-4">
          <div className="flex h-16 items-center justify-between">
            <div className="flex items-center space-x-8">
              <Link to="/" className="text-xl font-bold text-gray-900">
                AzerothCore Manager
              </Link>
              <Link 
                to="/stacks" 
                className="text-gray-600 hover:text-gray-900 transition"
              >
                Stacks
              </Link>
            </div>
            <Link
              to="/stacks/new"
              className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition"
            >
              Create Stack
            </Link>
          </div>
        </div>
      </nav>
      
      <main className="container mx-auto px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}

import { Link } from "react-router-dom";

export function NotFoundPage() {
  return (
    <div className="card flex flex-col items-center justify-center p-12 text-center">
      <div className="text-5xl font-bold text-slate-300">404</div>
      <h2 className="mt-2 text-xl font-semibold text-slate-900">Page not found</h2>
      <p className="mt-1 text-sm text-slate-500">The admin route you requested does not exist.</p>
      <Link to="/" className="btn-primary mt-6">Back to dashboard</Link>
    </div>
  );
}

import { BrowserRouter, Routes, Route } from 'react-router-dom';
import ProcessRunsPage from "./pages/ProcessRunsPage";
import ListPage from "./pages/ListPage";
import ListSearchPage from "./pages/ListSearchPage";
import InspectionReportPage from "./pages/InspectionReportPage";
import InspectionReportVerificationPage from "./pages/InspectionReportVerificationPage";

function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={<ProcessRunsPage />} />
                <Route path="/list" element={<ListPage />} />
                <Route path="/listSearch" element={<ListSearchPage />} />
                <Route path="/inspectionReport" element={<InspectionReportPage />} />
                <Route path="/inspectionReportVerification" element={<InspectionReportVerificationPage />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App

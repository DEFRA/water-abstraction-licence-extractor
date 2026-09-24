import { BrowserRouter, Routes, Route } from 'react-router-dom';
import ProcessRunsPage from "./pages/ProcessRunsPage";
import ListPage from "./pages/ListPage";
import ListSearchPage from "./pages/ListSearchPage";
import InspectionReportPage from "./pages/InspectionReportPage";

function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={<ProcessRunsPage />} />
                <Route path="/list" element={<ListPage />} />
                <Route path="/listSearch" element={<ListSearchPage />} />
                <Route path="/inspectionReport" element={<InspectionReportPage />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App

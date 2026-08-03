import { BrowserRouter, Routes, Route } from 'react-router-dom';
import ProcessRunsPage from "./pages/ProcessRunsPage";
import ListPage from "./pages/ListPage";
import ListSearchPage from "./pages/ListSearchPage";

function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={<ProcessRunsPage />} />
                <Route path="/list" element={<ListPage />} />
                <Route path="/listSearch" element={<ListSearchPage />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App

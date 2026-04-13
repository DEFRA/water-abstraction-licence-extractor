import { BrowserRouter, Routes, Route } from 'react-router-dom';
import ProcessRunsPage from "./pages/ProcessRunsPage";
import ListPage from "./pages/ListPage";

function App() {
    return (
        <BrowserRouter>
            <Routes>
                <Route path="/" element={<ProcessRunsPage />} />
                <Route path="/list" element={<ListPage />} />
            </Routes>
        </BrowserRouter>
    );
}

export default App

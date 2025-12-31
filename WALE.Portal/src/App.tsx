import { BrowserRouter, Routes, Route } from 'react-router-dom';
import ProcessRunsPage from "./pages/ProcessRunsPage.tsx";
import ListPage from "./pages/ListPage.tsx";

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

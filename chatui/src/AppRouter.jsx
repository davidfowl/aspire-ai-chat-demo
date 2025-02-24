import React from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import App from './components/App';

const AppRouter = () => (
    <BrowserRouter>
        <Routes>
            {/* Ensure the route parameter name is "chatId" */}
            <Route path="/chat/:chatId" element={<App />} />
            {/* Other routes */}
            <Route path="/" element={<App />} />
        </Routes>
    </BrowserRouter>
);

export default AppRouter;

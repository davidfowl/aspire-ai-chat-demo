import React from 'react';

const LandingPage: React.FC = () => {
    return (
        <div className="landing-container">
            <div className="landing-content">
                <h1>Chat with AI</h1>
                <div className="examples-grid">
                    <div className="example-card">
                        <h3>💡 Examples</h3>
                        <ul>
                            <li>"Explain quantum computing in simple terms"</li>
                            <li>"Got any creative ideas for a 10 year old's birthday?"</li>
                            <li>"How do I make an HTTP request in Javascript?"</li>
                        </ul>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LandingPage;

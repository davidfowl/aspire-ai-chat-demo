import React from 'react';

interface LandingPageProps {
    onExampleClick: (text: string) => void;
}

const examples = [
    "Explain quantum computing in simple terms",
    "Got any creative ideas for a 10 year old's birthday?",
    "How do I make an HTTP request in Javascript?"
];

const LandingPage: React.FC<LandingPageProps> = ({ onExampleClick }) => {
    return (
        <div className="landing-container">
            <div className="landing-content">
                <h1>Chat with AI</h1>
                <div className="examples-grid">
                    {examples.map((example, index) => (
                        <button
                            key={index}
                            className="example-card"
                            onClick={() => onExampleClick(example)}
                        >
                            <span className="example-emoji">💡</span>
                            <span className="example-text">{example}</span>
                        </button>
                    ))}
                </div>
            </div>
        </div>
    );
};

export default LandingPage;

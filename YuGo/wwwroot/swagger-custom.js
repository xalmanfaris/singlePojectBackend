(function () {
    const originalFetch = window.fetch;
    
    // Function to apply authorization to Swagger UI
    const authorizeSwagger = (token) => {
        if (window.ui && token) {
            window.ui.authActions.authorize({
                Bearer: {
                    name: "Bearer",
                    schema: {
                        type: "http",
                        scheme: "bearer",
                        in: "header",
                        name: "Authorization"
                    },
                    value: token
                }
            });
            console.log("Swagger UI authorized successfully.");
        }
    };

    // Check for existing token in localStorage on load
    const checkPersistedAuth = () => {
        const token = localStorage.getItem('swagger_token');
        if (token) {
            // Wait for Swagger UI to be ready
            const interval = setInterval(() => {
                if (window.ui) {
                    authorizeSwagger(token);
                    clearInterval(interval);
                }
            }, 500);
            
            // Timeout after 10 seconds
            setTimeout(() => clearInterval(interval), 10000);
        }
    };

    window.fetch = async function () {
        const response = await originalFetch.apply(this, arguments);
        const url = arguments[0];
        
        // Intercept requests to /api/Auth/login
        if (typeof url === 'string' && url.includes('/api/Auth/login') && response.ok) {
            const clone = response.clone();
            clone.json().then(data => {
                if (data && data.token) {
                    localStorage.setItem('swagger_token', data.token);
                    authorizeSwagger(data.token);
                }
            }).catch(err => console.error("Error parsing login response", err));
        }

        // Intercept logout to clear token
        if (typeof url === 'string' && url.includes('/api/Auth/logout')) {
            localStorage.removeItem('swagger_token');
            console.log("Logged out, cleared Swagger token.");
        }
        
        return response;
    };

    // Initialize on load
    if (document.readyState === 'complete') {
        checkPersistedAuth();
    } else {
        window.addEventListener('load', checkPersistedAuth);
    }
})();

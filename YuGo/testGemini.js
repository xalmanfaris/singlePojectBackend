const fs = require('fs');

async function testGemini() {
    const url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=AIzaSyDj4ZfFwjRwJOU3wv5p2M4KfuqjnsMsGgo";
    const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            contents: [{ parts: [{ text: "hello" }] }]
        })
    });
    console.log(res.status);
    console.log(await res.text());
}

testGemini();

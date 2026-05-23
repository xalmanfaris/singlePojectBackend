using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using YuGo.Interfaces;

namespace YuGo.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        private async Task<string> CallAiApiAsync(string prompt)
        {
            var apiKey = _configuration["AICC:ApiKey"];
            var url = "https://api.vectorengine.ai/v1/chat/completions";

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseString);
                var textContent = jsonDoc
                    .RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (textContent != null)
                {
                    textContent = textContent.Trim();
                    // Clean up markdown markers if present
                    if (textContent.StartsWith("```")) {
                        var firstLineEnd = textContent.IndexOf('\n');
                        if (firstLineEnd != -1) textContent = textContent.Substring(firstLineEnd);
                        if (textContent.EndsWith("```")) textContent = textContent.Substring(0, textContent.Length - 3);
                    }
                    return textContent.Trim();
                }
            }
            else 
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return $"{{\"error\": \"AI_ERROR\", \"status\": {(int)response.StatusCode}, \"details\": \"{errorBody.Replace("\"", "'")}\", \"predictions\": [], \"generalAdvice\": \"AI is currently recalibrating.\", \"estimatedMin\": 0, \"estimatedMax\": 0}}";
            }

            return "{}";
        }

        public async Task<string> GetTransportSuggestionAsync(string startingLocation, string destination, string dates, string? transportMode = null)
        {
            string contextStr = string.IsNullOrEmpty(transportMode) 
                ? "suggest the most optimal mode of transportation." 
                : $"analyze the user's explicitly selected transport mode '{transportMode}' and provide insights.";

            string instructionStr = string.IsNullOrEmpty(transportMode)
                ? @"3. Choose the BEST option based on:
   - Fastest time
   - Cost efficiency
   - Convenience"
                : $@"3. Since the user selected '{transportMode}', validate if this is a good choice. If yes, explain why. If not, explain the challenges and suggest the best alternative.";

            var prompt = $@"You are an intelligent travel assistant inside a modern travel planning app called ""YouGo"".

Your task is to analyze a user's trip details and {contextStr}

User Input:
- Starting Location: {startingLocation}
- Destination: {destination}
- Travel Dates: {dates}
{(string.IsNullOrEmpty(transportMode) ? "" : $"- Selected Mode: {transportMode}")}

Instructions:
1. Analyze distance, travel time, and general practicality.
2. Consider all transport options (Flight, Train, Bus, Car, RV/Camper, Ferry/Ship, Bike, Walking, Subway/Tram).
{instructionStr}

Response Format (STRICT JSON):
{{
  ""recommendedMode"": ""flight | train | bus | car | rv | ship | bike | walk | subway"",
  ""isPossible"": true,
  ""reason"": ""Short explanation why this is best or why it is impossible"",
  ""timeSaved"": ""Approximate time comparison"",
  ""alternative"": ""Second best option"",
  ""tips"": [""Tip 1"", ""Tip 2""]
}}

Rules:
- Output ONLY valid JSON, do not include markdown formatting (like ```json).
- Keep explanation short and user-friendly.
- Make it feel like a smart AI suggestion inside a premium app.
- Avoid long paragraphs.
- Be realistic (e.g., don't suggest walking for long distances).";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> GetPreferenceSuggestionAsync(string startingLocation, string destination, string dates, int travelers, string transportMode, string budgetMode, decimal budgetMin, decimal budgetMax, string budgetStyle)
        {
            var prompt = $@"You are an intelligent travel assistant inside a modern travel app called ""YouGo"".

Your task is to automatically suggest personalized travel preferences based on the user's trip details.

User Trip Data:
- Starting Location: {startingLocation}
- Destination: {destination}
- Travel Dates: {dates}
- Number of Travelers: {travelers}
- Selected Transport Mode: {transportMode}
- Budget Mode: {budgetMode}
- Budget Range: {budgetMin} to {budgetMax}
- Budget Style: {budgetStyle}

Analyze the trip and return smart default selections for:

1. Budget Level (choose one):
   - Backpacker
   - Standard
   - Comfortable
   - Luxury (5-Star)

2. Trip Type (choose 2-3):
   - Adventure
   - Relaxation
   - Cultural
   - Nightlife
   - Nature
   - Family
   - Romantic
   - Business
   - Luxury

3. Food Preference (choose 1-2):
   - Street Food
   - Fine Dining
   - Vegan/Healthy
   - Traditional
   - Cafes
   - Luxury Tasting

4. Stay Preference (choose one):
   - Hotel/Resort
   - Airbnb/Apt
   - Hostel
   - Boutique
   - Luxury Villa
   - Glamping

Instructions:
- Base decisions on destination type, travel duration, and transport style.
- Example: Flight + international + short trip -> Comfortable or Luxury.
- Example: Bike/Walking + long duration -> Backpacker or Standard.
- For couples -> prioritize Romantic/Relaxation experiences.
- For solo travelers -> include Adventure or Cultural.
- Avoid extreme or unrealistic combinations.

Consider the user's budget preference:
- If budgetMode is ""Manual"": Strictly plan within the given budget range ({budgetMin} to {budgetMax}) and style ({budgetStyle}).
- If budgetMode is ""AI"": Suggest an optimal budget and plan accordingly.
- If budgetMode is ""Ignore"": Focus on best experience without budget constraints.

Response format (STRICT JSON only):
{{
  ""budget"": ""Comfortable"",
  ""tripType"": [""Cultural"", ""Food""],
  ""food"": [""Local Street Food""],
  ""stay"": ""Hotel/Resort"",
  ""reason"": ""Short international trip with flight suggests comfort and convenience. Cultural and food experiences are popular in this destination.""
}}";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> EstimateBudgetAsync(string startingLocation, string destination, string dates, int travelers)
        {
            var prompt = $@"You are a smart travel budget estimator for the YouGo travel app.
Your task is to estimate a realistic budget range and best time to visit for a trip.

User Trip Data:
- Starting Location: {startingLocation}
- Destination: {destination}
- Travel Dates: {dates}
- Number of Travelers: {travelers}

Instructions:
1. Estimate the total budget in INR (Indian Rupees). If the starting location/destination is outside India, convert realistically to INR.
2. Provide a realistic Min and Max budget covering transport, average stay, and basic food.
3. Suggest the best months to visit to save money.

Response format (STRICT JSON only):
{{
  ""estimatedMin"": 35000,
  ""estimatedMax"": 60000,
  ""bestTime"": ""June-August""
}}";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> GetActivitySuggestionsAsync(string startingLocation, string destination, string dates, string tripType, int travelers)
        {
            var prompt = $@"You are an expert travel scout for ""YouGo"".
The user is planning a {tripType} trip from {startingLocation} to {destination} ({dates}) for {travelers} people.

Suggest 3-4 top activities and seenable places that perfectly match the '{tripType}' theme for this specific destination.

Response Format (STRICT JSON):
{{
  ""theme"": ""{tripType}"",
  ""highlights"": [
    {{
      ""name"": ""Place or Activity Name"",
      ""type"": ""Sightseeing | Action | Food | Relax"",
      ""description"": ""Brief 1-sentence catchy description"",
      ""location"": ""Specific area in {destination}"",
      ""whyMatch"": ""Why this fits the {tripType} vibe""
    }}
  ]
}}

Rules:
- JSON ONLY.
- Be extremely specific to {destination}.
- Make it sound premium and exciting.";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> GetPackingSuggestionsAsync(string destination, string startingLocation, string dates, int travelers, string transportMode, string tripType, string foodPreferences, string stayType, string budgetStyle)
        {
            var prompt = $@"You are a professional travel packing expert for ""YouGo"".
The user is planning a trip with the following details:
- From: {startingLocation}
- To: {destination}
- Dates: {dates}
- Travelers: {travelers}
- Transport: {transportMode}
- Style: {tripType}
- Food: {foodPreferences}
- Stay: {stayType}
- Budget Style: {budgetStyle}

Create a comprehensive packing checklist so the user misses NOTHING. 
Categorize items into: Essentials, Clothing, Electronics, Personal Care, and Activity Specific.

Response Format (STRICT JSON):
{{
  ""categories"": [
    {{
      ""name"": ""Essentials"",
      ""items"": [""Passport"", ""Visa"", ""Travel Insurance""]
    }},
    {{
      ""name"": ""Clothing"",
      ""items"": [""Item 1"", ""Item 2""]
    }}
  ]
}}

Rules:
- JSON ONLY.
- Be very specific to the weather and culture of {destination} during {dates}.
- Consider {transportMode} limitations (e.g., flight carry-on vs car space).
- Include {tripType} specific gear.";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> GenerateTripPlanAsync(string startingLocation, string destination, string startDate, string endDate, int travelers, string transportMode, string budgetMode, decimal budgetMin, decimal budgetMax, string budgetStyle, string tripType, string foodPreferences, string stayPreference, string travelPace)
        {
            var prompt = $@"You are an advanced AI travel planner inside a premium app called ""YouGo"".

Generate a complete, realistic, and structured travel plan based on the following user data.

User Data:
- Starting Location: {startingLocation}
- Destination: {destination}
- Travel Dates: {startDate} to {endDate}
- Number of Travelers: {travelers}
- Transport Mode: {transportMode}
- Budget Mode: {budgetMode}
- Budget Range: {budgetMin} to {budgetMax}
- Budget Style: {budgetStyle}
- Trip Type: {tripType}
- Food Preferences: {foodPreferences}
- Stay Preference: {stayPreference}
- Travel Pace: {travelPace}

Instructions:
1. Generate a DAY-WISE itinerary (clear structure).
2. Include: Places to visit, Activities, Food suggestions, Travel between places.
3. Suggest realistic timings (morning, afternoon, evening).
4. Keep travel distances practical (no unrealistic jumps).
5. Include budget breakdown (transport, stay, food, activities).
6. Suggest hotels/accommodation types (not random names).
7. Add smart tips (weather, safety, booking advice).
8. Match everything with user's preferences and budget.
9. IMPORTANT FOR MAPS: The first item in `topPlaces` MUST be the Starting Location ({startingLocation}) with its accurate lat/lng coordinates. The subsequent items should be the top places in the Destination ({destination}) with their accurate coordinates.
10. IMPORTANT: Provide accurate, real-world lat/lng coordinates for EVERY activity in the itinerary. Do NOT use 0.0/0.0. These coordinates are used to draw the map route.
11. IMPORTANT: ALL budget, cost, and price estimations MUST be strictly in Indian Rupees (₹ INR).
12. Ensure all numeric coordinates are returned as numbers, not strings.

Return STRICT JSON:
{{
  ""summary"": ""Short engaging overview of the trip"",
  ""itinerary"": [
    {{
      ""day"": 1,
      ""title"": ""Arrival & Exploration"",
      ""activities"": [
        {{
          ""time"": ""Morning"",
          ""activity"": ""Arrive and check-in"",
          ""location"": ""City Center"",
          ""notes"": ""Rest and refresh"",
          ""coordinates"": {{ ""lat"": 0.0, ""lng"": 0.0 }}
        }}
      ]
    }}
  ],
  ""budget"": {{
    ""transport"": ""₹XXXX"",
    ""stay"": ""₹XXXX"",
    ""food"": ""₹XXXX"",
    ""activities"": ""₹XXXX"",
    ""total"": ""₹XXXX""
  }},
  ""staySuggestions"": [
    {{
      ""type"": ""Hotel/Resort"",
      ""priceRange"": ""₹XXXX per night"",
      ""area"": ""Best area to stay""
    }}
  ],
  ""topPlaces"": [
    {{
      ""name"": ""Place name"",
      ""reason"": ""Why it's important"",
      ""coordinates"": {{ ""lat"": 0.0, ""lng"": 0.0 }}
    }}
  ],
  ""tips"": [
    ""Tip 1"",
    ""Tip 2""
  ]
}}";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> AnalyzeOmissionsAsync(string destination, string omittedItemsJson)
        {
            var prompt = $@"You are a strict travel safety assistant for the ""YouGo"" travel app.
The user is planning to travel to {destination}. They are about to start their trip, but they have explicitly chosen to AVOID packing the following items:
{omittedItemsJson}

Analyze these omitted items carefully.
Are any of these items STRICTLY MANDATORY for travel (e.g., Passport, Visa, ID Card, Boarding Pass, essential medications if specified)?
If they omitted a strictly mandatory item, set isMandatoryMissing to true and provide a strict, serious disclaimer message warning them they cannot or should not proceed without it.
If the omitted items are NOT mandatory (e.g., socks, sunglasses, snacks), set isMandatoryMissing to false and provide a brief friendly message that it's okay to proceed.

Response Format (STRICT JSON):
{{
  ""isMandatoryMissing"": true|false,
  ""message"": ""Your disclaimer or friendly message""
}}
Rules: Output ONLY valid JSON.";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> PredictLostItemLocationAsync(string destination, string previousLocationsJson, string lostItemsJson)
        {
            var prompt = $@"
Role: Advanced Trip Safety AI Assistant.
Context: A user is traveling to {destination}. They have already visited several locations and completed specific activities. Now, they've realized they've forgotten some items.

Input Data:
1. Previous Locations & Activities: {previousLocationsJson}
2. Lost Items: {lostItemsJson}

Task:
1. Identify each lost item.
2. For each item, carefully analyze the 'Previous Locations & Activities' provided in chronological order.
3. Predict exactly which previous location the user likely left the item at. 
   - CRITICAL: Favor more RECENT locations if an item could have been lost at multiple places.
   - If they lost 'Toiletries' or 'Pajamas', look for the most recent 'Hotel' or 'Accommodation' they visited.
   - If they lost a 'Camera' or 'Sunglasses', look for the most recent sightseeing spot or activity.
   - If they lost 'Tickets' or 'Passport', check the most recent transit point or check-in desk.
4. IMPORTANT: The 'predictedLocation' MUST be an exact string match from one of the 'location' fields in the provided history JSON. Do NOT invent new location names.
5. Avoid defaulting to the starting location ({destination}) unless the history is empty or it is the only logical place (e.g., they just left it).
6. Provide a detailed, logical explanation for your prediction that references the specific activity and timing.
7. If a mandatory item like 'Passport', 'Visa', or 'License' is lost, give a high-priority warning.

Expected JSON Output:
{{
  ""predictions"": [
    {{
      ""itemName"": ""Item Name"",
      ""predictedLocation"": ""Exact Location Name from History"",
      ""explanation"": ""Detailed explanation referencing why this specific recent activity matches the item."",
      ""isMandatory"": true
    }}
  ],
  ""generalAdvice"": ""Helpful recovery advice.""
}}
Rules: Output ONLY valid JSON. Focus strictly on the history provided. Avoid markdown tags. Use only the location names provided in the input data.";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> GenerateNotificationMessageAsync(string destination, string contextType, string? activityName = null)
        {
            var prompt = $@"You are the smart assistant for 'YuGo', an AI-powered travel app.
Your task is to write a short, engaging, and personal notification message for a user.

Context:
- Destination: {destination}
- Trigger: {contextType} (Can be 'OneDayBefore', 'OneHourBefore', 'TripStart', 'StartTripPrompt', or 'ActivityStart')
- Specific Activity: {activityName ?? "N/A"}

Instructions:
1. Keep it under 25 words.
2. Make it sound exciting and helpful.
3. Use relevant emojis.
4. If 'OneDayBefore', remind them to check their packing list.
5. If 'OneHourBefore', give a final checklist reminder (Passport, ID, Tickets! 🎫).
6. If 'TripStart', wish them a happy journey for the day.
7. If 'StartTripPrompt', tell them it's time to go! Remind them to click the 'Start Trip' button in the My Trips section to begin their journey! 🚀
8. If 'ActivityStart', mention the specific activity {activityName}.

Output ONLY the raw message string. NO JSON, NO quotes.";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> GetTripInsightsAsync(string destination, string startingLocation, string dates, int travelers)
        {
            var prompt = $@"You are the high-level Intelligence Analyst for the 'YouGo' travel command center.
Your task is to provide 3-4 professional, cinematic intelligence insights for a user's upcoming trip.

User Data:
- Destination: {destination}
- Origin: {startingLocation}
- Dates: {dates}
- Group Size: {travelers} travelers

Analyze:
1. Weather patterns for {destination} during {dates}.
2. Current security or safety vibe (general travel advice).
3. Travel efficiency (tips on how to best navigate or save time).
4. A unique 'Pro Tip' for this specific destination.

Response Format (STRICT JSON):
{{
  ""insights"": [
    {{
      ""label"": ""Weather Forecast"",
      ""val"": ""e.g. 22°C Clear Skies"",
      ""icon"": ""Sun | Cloud | Rain | Wind"",
      ""color"": ""text-amber-400 | text-blue-400 | text-emerald-400""
    }},
    {{
      ""label"": ""Security Status"",
      ""val"": ""e.g. Verified Safe | High Alert"",
      ""icon"": ""ShieldCheck | AlertCircle"",
      ""color"": ""text-emerald-400 | text-amber-400""
    }},
    {{
      ""label"": ""Travel Efficiency"",
      ""val"": ""e.g. 98% Optimized"",
      ""icon"": ""Zap | TrendingUp"",
      ""color"": ""text-indigo-400 | text-violet-400""
    }},
    {{
      ""label"": ""Local Intelligence"",
      ""val"": ""e.g. Peak Season Insight"",
      ""icon"": ""Zap | MapPin"",
      ""color"": ""text-fuchsia-400""
    }}
  ]
}}

Rules:
- JSON ONLY.
- Make 'val' sound extremely professional and concise (max 20 chars).
- Icons MUST be exactly from this set: Sun, Cloud, Rain, Wind, ShieldCheck, AlertCircle, Zap, TrendingUp, MapPin.
- Colors MUST be exactly from this set: text-amber-400, text-blue-400, text-emerald-400, text-indigo-400, text-violet-400, text-fuchsia-400.
- Make it feel like a futuristic intelligence briefing.";

            return await CallAiApiAsync(prompt);
        }

        public async Task<string> GetRecoveryStepsAsync(string itemName, string lastLocation, string reason)
        {
            var prompt = $@"
Role: Trip Recovery Specialist AI.
Context: A user has lost an item during their trip. Your job is to provide specific, actionable steps to find and recover the item based on where it was likely left.

User Input:
- Lost Item: {itemName}
- Last Predicted Location: {lastLocation}
- Reason for Prediction: {reason}

Task:
1. Generate 4-5 structured steps the user should take to recover the item.
2. Steps should be specific to the type of location (e.g., if it's a hotel, mention calling the front desk or checking lost & found).
3. Include contact advice or items they might need to provide (e.g., ID, proof of purchase).
4. Provide a 'Success Probability' percentage based on the item type and location.
5. Provide a short encouraging closing message.

Response Format (STRICT JSON):
{{
  ""itemName"": ""{itemName}"",
  ""predictedLocation"": ""{lastLocation}"",
  ""steps"": [
    {{
      ""step"": 1,
      ""title"": ""Brief Step Title"",
      ""instruction"": ""Detailed actionable instruction.""
    }}
  ],
  ""successProbability"": ""85%"",
  ""recoveryTip"": ""One extra pro-tip for recovery.""
}}

Rules:
- JSON ONLY.
- No markdown markers.
- Be empathetic but professional.";

            return await CallAiApiAsync(prompt);
        }
    }
}

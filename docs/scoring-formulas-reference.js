// Main function
function calculate() {
    let gender = document.getElementById("gender").value;
    let benchonly = document.getElementById("check").value;
    let total = document.getElementById("total").value;
    let Bw = document.getElementById("weight").value;
    let class_name = document.getElementById("class").value;
    let mass = document.getElementById("mass").value;
    
    if (mass == "pounds") {
        total = (total / 2.20462);
        Bw = (Bw/2.20462);
    }

    // check if input values are valid
    if (!gender || !benchonly || !total || !Bw || !class_name) {
        showError("Please fill in all the input fields");
        return;
    }
    else if (isNaN(total) || isNaN(Bw)) {
        showError("Please enter a valid number for total and weight.");
        return;
    }
    else if (gender !== "male" && gender !== "female") {
        showError("Please enter a valid gender (male or female).");
        return;
    }
    else if (benchonly !== "yes" && benchonly !== "no") {
        showError("Please enter a valid value for bench press only (yes or no).");
        return;
    }
    else if (class_name !== "classic" && class_name !== "equipped") {
        showError("Please enter a valid class (classic or equipped).");
        return;
    }
    else if (Bw<=0) {
        showError("Please enter a valid bodyweight value.");
    }
    else if (total<=0) {
        showError("Please enter a valid total value.");
    }
    else {

    document.getElementById("error-message").innerHTML = "";

    let IPF_GL = IPF_GL_calculator(Bw, gender, class_name, total, benchonly);
    let Dots_points = Dots_calculator(Bw, gender, total);
    let Wilks1 = Wilks1_calculator(Bw, gender, total);
    let Wilks2 = Wilks2_calculator(Bw, gender, total);
    let IPF_points = IPF_points_calculator(Bw, gender, class_name, total, benchonly);
    let Check1 = "Invalid";

    if (benchonly == "yes") { 
        document.getElementById("IPF_GL").innerHTML = IPF_GL;
        document.getElementById("Dots_points").innerHTML = Check1;
        document.getElementById("Wilks1").innerHTML = Check1;
        document.getElementById("Wilks2").innerHTML = Check1;
        document.getElementById("IPF_points").innerHTML = IPF_points;

    } else {
        document.getElementById("IPF_GL").innerHTML = IPF_GL;
        document.getElementById("Dots_points").innerHTML = Dots_points;
        document.getElementById("Wilks1").innerHTML = Wilks1;
        document.getElementById("Wilks2").innerHTML = Wilks2;
        document.getElementById("IPF_points").innerHTML = IPF_points;
    }
    }
}


// IPF coefficients
const coefficients1 = {
    "male_equipped_powerlifting": {"A": 1236.25115, "B": 1449.21864, "C": 0.01644},
    "male_classic_powerlifting": {"A": 1199.72839, "B": 1025.18162, "C": 0.00921},
    "male_equipped_bench_press": {"A": 381.22073, "B": 733.79378, "C": 0.02398},
    "male_classic_bench_press": {"A": 320.98041, "B": 281.40258, "C": 0.01008},
    "female_equipped_powerlifting": {"A": 758.63878, "B": 949.31382, "C": 0.02435},
    "female_classic_powerlifting": {"A": 610.32796, "B": 1045.59282, "C": 0.03048},
    "female_equipped_bench_press": {"A": 221.82209, "B": 357.00377, "C": 0.02937},
    "female_classic_bench_press": {"A": 142.40398, "B": 442.52671, "C": 0.04724},
};

// Dots coefficients
const coefficients2 = {
    "male": {
        "A": -0.000001093,
        "B": 0.0007391293,
        "C": -0.1918759221,
        "D": 24.0900756,
        "E": -307.75076
    },
    "female": {
        "A": -0.0000010706,
        "B": 0.0005158568,
        "C": -0.1126655495,
        "D": 13.6175032,
        "E": -57.96288
    }
};

// Wilks1 coefficients
const coefficients3 = {
    "male": {
        "A": -0.00000001291,
        "B": 0.00000701863,
        "C": -0.00113732,
        "D": -0.002388645,
        "E": 16.2606339,
        "F": -216.0475144
    },
    "female": {
        "A": -0.00000009054,
        "B": 0.00004731582,
        "C": -0.00930733913,
        "D": 0.82112226871,
        "E": -27.23842536447,
        "F": 594.31747775582
    }
};


// Wilks2 coefficients

const coefficients4 = {
    "male": {
        "A": -0.0000000120804336482315,
        "B": 0.00000707665973070743,
        "C": -0.00139583381094385,
        "D": 0.073694103462609,
        "E": 8.47206137941125,
        "F": 47.4617885411949
    },
    "female": {
        "A": -0.000000023334613884954,
        "B": 0.00000938773881462799,
        "C": -0.0010504000506583,
        "D": -0.0330725063103405,
        "E": 13.7121941940668,
        "F": -125.425539779509
    }
};


//IPF Points coefficients
const coefficients5 = {
    "male": {
        "classic Powerlifting": {"A": 310.6700, "B": 857.7850, "C": 53.2160, "D": 147.0835},
        "classic Bench Press": {"A": 86.4745, "B": 259.1550, "C": 17.5785, "D": 53.1220},
        "equipped Powerlifting": {"A": 387.2650, "B": 1121.2800, "C": 80.6324, "D": 222.4896},
        "equipped Bench Press": {"A": 133.9400, "B": 441.4650, "C": 35.3938, "D": 113.0057}
    },
    "female": {
        "classic Powerlifting": {"A": 125.1435, "B": 228.0300, "C": 34.5246, "D": 86.8301},
        "classic Bench Press": {"A": 25.0485, "B": 43.8480, "C": 6.7172, "D": 13.9520},
        "equipped Powerlifting": {"A": 176.5800, "B": 373.3150, "C": 48.4534, "D": 110.0103},
        "equipped Bench Press": {"A": 49.1060, "B": 124.2090, "C": 23.1990, "D": 67.4926}
    }
};


// IPF GL calculator
function IPF_GL_calculator(Bw, gender, class_name, total, benchonly) {
    if (benchonly == "yes") {
        class_name = gender + "_" + class_name + "_bench_press"
    } else {
        class_name = gender + "_" + class_name + "_powerlifting"
    }
    A = coefficients1[class_name]["A"]
    B = coefficients1[class_name]["B"]
    C = coefficients1[class_name]["C"]
    IPF_coefficient = 100 / (A- (B * Math.exp(-C*Bw)))
    let IPF_GL = IPF_coefficient * total    
    return IPF_GL.toFixed(2)
}


// Dots calculator
function Dots_calculator(Bw, gender, total) {
    A = coefficients2[gender]["A"]
    B = coefficients2[gender]["B"]
    C = coefficients2[gender]["C"]
    D = coefficients2[gender]["D"]
    E = coefficients2[gender]["E"]
    Dots_coefficient = 500 / (A * Math.pow(Bw,4) + B * Math.pow(Bw,3) + C * Math.pow(Bw,2) + D * Bw + E)
    let Dots_points = total * Dots_coefficient
    return Dots_points.toFixed(2)
}


// Wilks 1 (original) calculator
function Wilks1_calculator(Bw, gender, total) {
    A = coefficients3[gender]["A"]
    B = coefficients3[gender]["B"]
    C = coefficients3[gender]["C"]
    D = coefficients3[gender]["D"]
    E = coefficients3[gender]["E"]
    F = coefficients3[gender]["F"]
    Wilks1_coefficient = (500 / (F + E * Bw + D * Math.pow(Bw, 2) + C * Math.pow(Bw, 3) + B * Math.pow(Bw, 4) + A * Math.pow(Bw, 5)))
    let Wilks1 = total * Wilks1_coefficient
    return Wilks1.toFixed(2)
} 


// Wilks 2 (2020 rework) calculator
function Wilks2_calculator(Bw, gender, total) {
    A = coefficients4[gender]["A"]
    B = coefficients4[gender]["B"]
    C = coefficients4[gender]["C"]
    D = coefficients4[gender]["D"]
    E = coefficients4[gender]["E"]
    F = coefficients4[gender]["F"]
    Wilks2_coefficient = (600 / (F + E * Bw + D * Math.pow(Bw, 2) + C * Math.pow(Bw, 3) + B * Math.pow(Bw, 4) + A * Math.pow(Bw, 5)))
    let Wilks2 = total * Wilks2_coefficient
    return Wilks2.toFixed(2)
}


// IPF_points calculator
function IPF_points_calculator(Bw, gender, class_name, total, benchonly) {
    if (benchonly == "yes") {
      class_name = class_name + " Bench Press";
    } else {
      class_name = class_name + " Powerlifting";
    }
    A = coefficients5[gender][class_name]["A"]
    B = coefficients5[gender][class_name]["B"]
    C = coefficients5[gender][class_name]["C"]
    D = coefficients5[gender][class_name]["D"]
    IPF_point_coefficient = ((total - (A * Math.log(Bw) - B)) / (C * Math.log(Bw) - D))
    let IPF_points = (500 + 100 * (IPF_point_coefficient))
    return IPF_points.toFixed(2)
}

document.addEventListener('keyup', function(event) {
    if (event.code === 'Enter') {
        calculate();
    }
});

// This function will be called when the calculate button is clicked
document.getElementById('calculate').addEventListener('click', calculate);



// Error function
function showError(message) {
    let errorElement = document.getElementById("error-message");
    errorElement.innerHTML = message;
}

function setRandomGradient() {
    // Generate random RGB values for the two colors
    const color1 = `rgb(${Math.floor(Math.random() * 256)}, ${Math.floor(Math.random() * 256)}, ${Math.floor(Math.random() * 256)})`;
    const color2 = `rgb(${Math.floor(Math.random() * 256)}, ${Math.floor(Math.random() * 256)}, ${Math.floor(Math.random() * 256)})`;

    // Set the gradient background with the generated colors
    document.body.style.background = `linear-gradient(240deg, ${color1}, ${color2})`;
}

// Call the function to set the gradient when the page loads
window.onload = setRandomGradient;

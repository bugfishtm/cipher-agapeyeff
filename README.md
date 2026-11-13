# D'Agapeyeff Cipher Investigation

## 🔍 Overview

This repository hosts my investigation of the **D'Agapeyeff cipher** — the challenge cryptogram on page 159 of Alexander d'Agapeyeff's *Codes and Ciphers* (1939), unsolved to this day. The write-up is served via **GitHub Pages** at [https://bugfishtm.github.io/cipher-agapeyeff](https://bugfishtm.github.io/cipher-agapeyeff) and walks through the cryptogram, the measurements, every attack that has been run against it, and what is left to try.

The repository also contains the native attack harness (`solver/`) that produced every negative result quoted on the site — 222 million transposition keys rejected, each attack first validated against a known control.

## ⚠️ How This Documentation Came to Be

The content in this repository is **my own work from the past** — notes, experiments, workbench tools and algorithms that had piled up unsorted over time. I used **AI to sort that material** and to write it up as a website that is easy to look through. So most of the wording is not mine — **the ideas behind it, the programmed workbench tools and the algorithms are**.

Parts of this work deliberately follow a single possibility all the way to its end, including ones that may look like the wrong track — testing and ruling out such a path is part of the work itself. Because of that focus, and because the text was written by AI, it can sometimes sound blunt, dismiss other ideas or explanations, or come across as disrespectful. **None of that is meant personally.** If your idea or theory seems brushed aside here, please don't take offence — it is not a judgement of you or of your work.

**Please note:** This is a private project. The source code is public only because it is part of my project infrastructure — GitHub Pages requires the repository content to be accessible. This is not an open-source project, not a template, and not intended for reuse.

## 📁 Repository Structure

This table provides an overview of the key files and folders of the project.

|Path|Description|
|----|-----|
| docs/ | The investigation website, served via GitHub Pages. |
| docs/index.html | Home page — case status, the argument in six lines, section overview. |
| docs/01-cryptogram.html … 10-book.html | One page per section — cryptogram, measurements, evidence, attacks, verdict, published record, next steps, assessment, the book's toolkit. |
| docs/11-workbench.html | Interactive workbench — five tools and two solvers running in the browser. |
| docs/assets/ | Stylesheet, scripts and cipher data — everything served locally. |
| solver/ | Native C# (.NET 10) cryptanalysis harness. See [solver/README.md](solver/README.md) for build instructions and all attack modes. |
| solver/corpus.txt | Language-model corpus — the text of the book itself. |
| solver/words.txt | Candidate transposition keys — the book's vocabulary, names and 1939 dates. |
| [LICENSE.md](LICENSE.md) | License of this project. |

## 👀 Looking Around

You are welcome to browse the code, read the investigation and see how the attacks are built. However:

- **All rights are reserved.** No permission is granted to copy, reuse, modify or redistribute any part of this repository — code, design, texts or data — unless explicitly stated otherwise in [LICENSE.md](LICENSE.md).
- This repository does not accept feature requests, and contributions are generally not expected — it exists to document my own work on the cipher.

## 📜 License Information

The license for this project can be found in the [LICENSE.md](LICENSE.md) file. The repository may also include additional licensed software or libraries.

🐟 Bugfish

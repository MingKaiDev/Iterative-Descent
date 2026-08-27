# BKT Concept Map — AI-Enabled Serious Game

## Overview
50 sub-concepts across 6 categories. BKT tracks one `P(know)` value per concept node.
Prerequisite-aware: a concept is only presented to the player once all its prerequisites exceed a threshold P(know) (suggested: 0.7).

---

## Category 1 — Foundations (16 concepts)
Entry-point category. No category-level prerequisites. All other advanced categories depend on concepts here.

| Concept | Prerequisites |
|---|---|
| Arrays & Lists | — |
| Linked Lists ✓ (already in game) | Arrays & Lists |
| Stacks & Queues | Arrays & Lists |
| Hash Tables | Arrays & Lists |
| Trees (BST/AVL) | Linked Lists |
| Heaps | Trees |
| Graphs | Trees |
| BFS / DFS | Graphs |
| Dijkstra's Algorithm ✓ (already in game) | BFS / DFS |
| Dynamic Programming | Graphs |
| Greedy Algorithms | Arrays & Lists |
| Basic Sorts (Bubble/Insertion/Selection) | Arrays & Lists |
| Merge Sort | Basic Sorts |
| Quick Sort | Merge Sort |
| Heap Sort | Heaps |
| Complexity (Big-O) | Arrays & Lists |

---

## Category 2 — Systems & Architecture (9 concepts)

| Concept | Prerequisites |
|---|---|
| CPU & Memory Basics | — |
| Cache & Memory Hierarchy | CPU & Memory Basics |
| OSI Model & Protocols | CPU & Memory Basics |
| TCP/IP | OSI Model & Protocols |
| Processes & Threads | CPU & Memory Basics |
| Memory Management | Processes & Threads |
| File Systems | Memory Management |
| CPU Scheduling | Processes & Threads |
| Concurrency / HPC | Processes & Threads |

---

## Category 3 — SWE Advanced (9 concepts)

| Concept | Prerequisites |
|---|---|
| OOP Basics | — |
| Inheritance & Polymorphism | OOP Basics |
| Design Patterns | Inheritance & Polymorphism |
| SQL & Relational Model | OOP Basics |
| Normalisation | SQL & Relational Model |
| NoSQL & Transactions | SQL & Relational Model |
| Agile / SDLC | — |
| Web Dev Basics | OOP Basics |
| Testing & QA | Agile / SDLC |

---

## Category 4 — Cybersecurity (6 concepts)

| Concept | Prerequisites |
|---|---|
| Symmetric Encryption | TCP/IP |
| Asymmetric Encryption | Symmetric Encryption |
| Hashing | Symmetric Encryption |
| PKI & Certificates | Asymmetric Encryption |
| OWASP / Web Vulnerabilities | Web Dev Basics + Hashing |
| Security Frameworks | OWASP / Web Vulnerabilities |

---

## Category 5 — Artificial Intelligence (6 concepts)

| Concept | Prerequisites |
|---|---|
| ML Fundamentals | Complexity (Big-O) + Graphs |
| Supervised Learning | ML Fundamentals |
| Neural Networks | Supervised Learning |
| Unsupervised Learning | ML Fundamentals |
| Reinforcement Learning | Supervised Learning + Dynamic Programming |
| Search & Planning | BFS / DFS |

---

## Category 6 — Misc (4 concepts)

| Concept | Prerequisites |
|---|---|
| CS History & Turing | — |
| Boolean Logic & Gates | — |
| Version Control (Git) | — |
| CLI & Debugging | — |

---

## Cross-Category Prerequisite Notes

These concepts span multiple categories — important for room gating logic:

- **Reinforcement Learning** ← Supervised Learning (AI) + Dynamic Programming (Foundations)
- **OWASP / Web Vulnerabilities** ← Web Dev Basics (SWE) + Hashing (Cybersecurity)
- **ML Fundamentals** ← Complexity/Big-O (Foundations) + Graphs (Foundations)
- **Cybersecurity** as a whole is gated behind Systems (TCP/IP)

---

## Content Targets

Minimum questions per concept for reliable BKT: **5**
Full map target: 50 × 5 = **250+ MCQ questions**

**Priority for implementation:**
1. Foundations category first (16 concepts × 5 = 80 MCQs) — linked list puzzle already covers one
2. SWE Advanced second (OOP, SQL — closest to existing puzzle content)
3. Remaining categories in later sprints

---

## Implementation Notes

- Each concept node stores: `P_L0` (prior), `P_T` (transit), `P_S` (slip), `P_G` (guess)
- Default starting parameters: P_L0=0.3, P_T=0.1, P_S=0.1, P_G=0.25
- Prerequisite gate threshold: P(know) ≥ 0.70 before dependent concept unlocks
- Question selection: target concept with lowest P(know) among all unlocked concepts
- See `bkt-model.md` for full implementation design once written

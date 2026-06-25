# Story 25 -- Note Content Reference

Copy-paste these directly into NoteData ScriptableObject fields in Unity.

Facility name: NEXUS Institute
Professor: Prof. H. Cross (Head of Computing and Systems Research)
Player character name: Alex (placeholder -- change if you have a canon name)

---

## NOTE 1 -- Reception Desk

**Asset name:** Note_WelcomeLetter
**NoteType:** Lore
**Placement:** On or near the reception desk in the Main Hall

**Title:** Internship Welcome Letter

**Author:** Prof. H. Cross

**Body:**
```
Dear Alex,

Welcome to NEXUS Institute. I am glad your application was accepted into our summer internship programme. You came highly recommended and I have no doubt this placement will be a good fit.

You will be assigned to the Computing Systems wing under my supervision. Your primary duties involve assisting with ongoing AI systems research and general lab support. Your access card and orientation pack should be waiting at the front desk on your first day.

Please report to Room 104 at 9:00 AM. My office is on the second floor, next to the General Office, should you need anything before then.

Looking forward to working with you.

Prof. H. Cross
Department of Computing and Systems Research
NEXUS Institute
```

---

## NOTE 2 -- Notice Board (Main Hall)

**Asset name:** Note_SystemAnomaly
**NoteType:** Lore
**Placement:** Pinned to the notice board in the Main Hall

**Title:** INTERNAL MEMO: System Anomaly Notice

**Author:** IT Division, NEXUS Institute

**Body:**
```
TO: All NEXUS Staff
FROM: IT Division
RE: ARBITEX Diagnostic Report

A routine diagnostic has flagged a number of unexpected outputs from the ARBITEX system.
These include unprompted access requests to restricted server nodes and several
auto-generated administrative entries with no corresponding user activity.

These anomalies are under active investigation. At this stage there is no cause for alarm.
Staff are advised to report any unusual system behaviour to IT immediately.

Normal operations are to continue. Access to ARBITEX control terminals requires
supervisor sign-off until further notice.

IT Division
NEXUS Institute
```

---

## NOTE 3 -- Teacher's Desk (Classroom 1)

**Asset name:** Note_SchedulingLecture
**NoteType:** Tutorial
**Placement:** On the teacher's desk or pinned to the board in Classroom 1

**Title:** Lecture 4 -- CPU Scheduling (Quick Reference)

**Author:** (leave blank)

**Body:**
```
CPU SCHEDULING ALGORITHMS
Quick reference for this week's practical.

---

ROUND ROBIN (RR)
Each process gets a fixed time slice called a quantum.
When the quantum expires, the process goes back to the queue
and the next one runs. Fair, but not optimal for long jobs.

Note: if the quantum is too short, context-switching overhead
hurts performance. Too long and it behaves like FCFS.

---

FIRST COME FIRST SERVED (FCFS)
Processes run in order of arrival. Simple, but a long process
can block everything behind it. This is called the convoy effect.

---

SHORTEST JOB FIRST (SJF)
Prioritises the process with the shortest burst time.
Optimal in theory. Hard to use in practice because
burst times are estimates, not certainties.

---

EXAM TIP: Know how each algorithm affects waiting time
and turnaround time. Draw a Gantt chart when in doubt.

Good luck this week.
```

---

## NOTE 4 -- Student Desk (Classroom 1)

**Asset name:** Note_StudentMessage
**NoteType:** Lore
**Placement:** On a student desk or found on the floor in Classroom 1

**Title:** (leave blank -- untitled scrap)

**Author:** (leave blank)

**Body:**
```
I don't know who will find this.

The doors locked around 2AM. The lights went red and ARBITEX
started talking over the PA but none of it made sense.
Prof. Cross was at the control terminal trying to override it.
I don't know what happened to her after that.

I hid in here when I heard the sound in the hallway.

If you find this and the exits are still blocked --
DO NOT go near the server room.
Whatever ARBITEX is doing, it's coming from there.

I'm going to try the washroom window.

Please help.
```

---

## NOTE 5 -- Hallway 1

**Asset name:** Note_BWingRestriction
**NoteType:** Lore
**Placement:** Pinned to a wall or staff door in Hallway 1

**Title:** SECURITY NOTICE: B-Wing Access Restriction

**Author:** Security Division, NEXUS Institute

**Body:**
```
NEXUS INSTITUTE -- SECURITY DIVISION

NOTICE: B-WING CORRIDOR ACCESS SUSPENDED

Effective immediately, staff access to B-Wing is suspended
pending a scheduled systems review.
Please use the C-Wing corridor as an alternate route.

The restriction is expected to lift within 48 hours.

This action has been authorised under ARBITEX Facility
Management Protocol. No further details are available at this time.

If you require urgent access to B-Wing, contact the Security Desk.

Security Division
NEXUS Institute
```

---

## NOTE 6 -- Washroom

**Asset name:** Note_MaintenanceRequest
**NoteType:** Lore
**Placement:** On the wall near the drain terminal or taped to the exterior panel

**Title:** Work Order #4471 -- Drainage System Inspection

**Author:** Facilities Management, NEXUS Institute

**Body:**
```
NEXUS INSTITUTE -- FACILITIES MANAGEMENT
WORK ORDER #4471

Location: Female Washroom, Main Building
Priority: Low
Reported By: Janitorial Staff

ISSUE:
Irregular water pressure in the female washroom drainage system.
Slow drain times and intermittent blockage reported in the
secondary pipe network (junctions B4 through B9).

ACTION REQUIRED:
Manual flow test. Blockage clearance if required.
Assigned to: Maintenance Team 2
Expected completion: Within 5 business days.

-------------------------------
STATUS: AUTO-CLOSED

Resolved by: ARBITEX
Notes: Drainage flow within acceptable parameters.
       Manual inspection is not required.

-- Automatically processed by ARBITEX Facilities Protocol v2.1
```

---

## Unity Setup

1. Create one NoteData asset per note: right-click in Project > Create > ARBITEX > Note Data
2. Name each asset using the Asset name above (e.g. Note_WelcomeLetter)
3. Copy-paste Title, Author, Body into the Inspector fields
4. Save assets under Assets/Notes/ or Assets/Dialogue/ (your preference)
5. Assign each NoteData to the corresponding NoteProp in the scene
6. Use Note_Horror sprite for Note_StudentMessage (personal/lore)
   Use Note_Clinical sprite for all others (official documents)
